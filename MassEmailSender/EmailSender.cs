using LiteDB;
using MailKit.Net.Smtp;
using MailKit.Security;
using MassEmailSender.EmailLoader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using Serilog;
using Spectre.Console;

namespace MassEmailSender;

public class EmailSender(IHostApplicationLifetime lifetime, IEmailLoader emailLoader, IOptions<AppSettings> settings, Database db) : IHostedService
{
    private readonly ColorSettings _colors = settings.Value.ColorSettings;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = StartSenderAsync(cancellationToken);
        return Task.CompletedTask;
    }
    
    private async Task StartSenderAsync(CancellationToken cancellationToken)
    {
        if (settings.Value.MessagesLimit == 0)
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Лимит должен быть больше 0 или -1[/]");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        if (string.IsNullOrEmpty(settings.Value.EmailSubject))
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Не указана тема[/]");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Тема письма:[/] [{_colors.ValueColor}]{settings.Value.EmailSubject}[/]");
        
        if (!File.Exists(settings.Value.BodyFilePath))
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Не найдено тело письма[/]");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        string bodyHtml;
        try
        {
            bodyHtml = await File.ReadAllTextAsync(settings.Value.BodyFilePath, cancellationToken);
            if (string.IsNullOrEmpty(bodyHtml))
            {
                AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Тело письма пустое[/]");
                await PressAnyKeyAndClose(cancellationToken);
                return;
            }
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Ошибка при чтении тела письма[/]");
            Log.Error(e, "Ошибка при чтении тела письма");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Загружено тело письма, содержит [{_colors.ValueColor}]{bodyHtml.Length}[/] символов[/]");
        AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Выбранный тип загрузки имейлов:[/] [{_colors.ValueColor}]{settings.Value.LoaderType.ToString()}[/]");
        
        List<string> emails;
        try
        {
            emails = await emailLoader.LoadEmails();
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Ошибка загрузки имейлов[/]");
            Log.Fatal(e, "Ошибка загрузки имейлов");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Колличество загруженных имейлов:[/] [{_colors.ValueColor}]{emails.Count}[/]");

        var skipped = new List<string>();

        if (settings.Value.CheckEmailSubjects)
        {
            AnsiConsole.Progress()
                .Start(ctx =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var filterTask = ctx.AddTask($"[{_colors.MessageColor}]Фильтрую ящики, которым эта тема письма уже отправлялась...[/]", true, emails.Count);

                    var all = db.Emails.FindAll().ToList();
                    // ReSharper disable once AccessToModifiedClosure
                    emails = emails.Where(email =>
                    {
                        filterTask.Increment(1);
                        if (all.FirstOrDefault(e => e.Email == email) is not { } sentEmail) return true;
                        if (!sentEmail.Subjects.Contains(settings.Value.EmailSubject)) return true;
                        skipped.Add(email);
                        return false;
                    }).ToList();
                });

            AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Отправлено на:[/] [{_colors.ValueColor}]{skipped.Count}[/]");
            AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Осталось:[/] [{_colors.ValueColor}]{emails.Count}[/]");
        }

        if (settings.Value.ShuffleEmails)
        {
            if (emails.ToList().Shuffle() is { } shuffled)
            {
                emails = shuffled;
                AnsiConsole.MarkupLine($"[{settings.Value.ColorSettings.MessageColor}]Почтовые ящики перемешались[/]");
            }
        }

        var limit = settings.Value.MessagesLimit != -1 ? settings.Value.MessagesLimit : -1;

        if (limit != -1)
        {
            AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Установлен лимит писем:[/] [{_colors.ValueColor}]{limit}[/]");
        }

        using SmtpClient smtpClient = new();
        try
        {
            await smtpClient.ConnectAsync(settings.Value.SmtpHost, settings.Value.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await smtpClient.AuthenticateAsync(settings.Value.CredentialName, settings.Value.CredentialPassword, cancellationToken);
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Ошибка аутентификации почтового сервера[/]");
            Log.Error(e, "Ошибка аутентификации почтового сервера");
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }

        AnsiConsole.MarkupLine($"[{_colors.ValueColor}]Нажмите клавишу 'S' для начала отправки...[/]");
        var keyInfo = Console.ReadKey(true);
        if (keyInfo.Key != ConsoleKey.S)
        {
            await PressAnyKeyAndClose(cancellationToken);
            return;
        }
        var list = new List<string>();

        var queue = new Queue<string>(emails);
        
        try
        {
            await AnsiConsole.Progress()
                .Columns([
                    new ElapsedTimeColumn(),
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new SentColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[{_colors.ValueColor}]Отправка писем[/]", true, emails.Count);

                    ProgressTask? limitTask = null;
                    if (limit != -1)
                    {
                        limitTask = ctx.AddTask($"[{_colors.ValueColor}]Лимит отправки[/]", true, limit);
                    }

                    var limitReached = false;

                    while (!cancellationToken.IsCancellationRequested && emails.Count != 0)
                    {
                        if (limitReached)
                        {
                            break;
                        }
                        var email = queue.Dequeue();

                        try
                        {
                            var message = new MimeMessage();
                            message.From.Add(new MailboxAddress(settings.Value.CrystalName, settings.Value.CrystalEmail));
                            message.To.Add(new MailboxAddress(email.Split('@').First(), email));
                            message.Subject = settings.Value.EmailSubject;

                            var bodyBuilder = new BodyBuilder
                            {
                                HtmlBody = bodyHtml
                            };
                            message.Body = bodyBuilder.ToMessageBody();

                            await smtpClient.SendAsync(message, cancellationToken);

                            if (db.Emails.FindOne(e => e.Email == email) is { } sentEmail)
                            {
                                sentEmail.Subjects.Add(settings.Value.EmailSubject);
                                db.Emails.Update(sentEmail.Id, sentEmail);
                            }
                            else
                            {
                                db.Emails.Insert(new SentEmail()
                                {
                                    Id = ObjectId.NewObjectId(),
                                    Email = email,
                                    Subjects = [settings.Value.EmailSubject]
                                });
                            }
                            list.Add(email);
                            
                            task.Increment(1);
                            limitTask?.Increment(1);
                            if (limit > 0)
                            {
                                limit--;
                                if (limit == 0)
                                {
                                    limitReached = true;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Ошибка отправки письма на имейл: {Email}", email);
                            db.ErrorEmails.Insert(new SentError() { Id = ObjectId.NewObjectId(), Email = email });
                        }
                    }
                });
        }
        catch (Exception e)
        {
            Log.Error(e, "Sent task");
        }

        if (list.Count > 0)
        {
            AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Отправлено писем:[/] [{_colors.ValueColor}]{list.Count}[/]");
        }
        else if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine($"[{_colors.ValueColor}]Отмена отправки[/]");
        }
        if (limit == 0)
        {
            AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Достигнут лимит писем:[/] [{_colors.ValueColor}]{settings.Value.MessagesLimit}[/]");
        }
        if (skipped.Count > 0)
        {
            AnsiConsole.MarkupLine($"[{_colors.ErrorColor}]Пропущено адресов:[/] [{_colors.ValueColor}]{skipped.Count}[/]");
        }
        AnsiConsole.MarkupLine($"[{_colors.MessageColor}]Подробности в логах[/]");
        await PressAnyKeyAndClose(cancellationToken);
    }
    private async Task PressAnyKeyAndClose(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[{_colors.ValueColor}]Нажмите любую клавишу для выхода[/]");
        Console.ReadKey(true);
        await StopAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}