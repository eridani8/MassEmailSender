using MassEmailSender;
using MassEmailSender.EmailLoader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.File($"{logsPath}/errors-.log", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder();

    var settings = builder.Configuration.GetSection(nameof(AppSettings)).Get<AppSettings>()!;
    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));
    builder.Services.AddSingleton<Database>();
    switch (settings.LoaderType)
    {
        case LoaderType.Csv:
            builder.Services.AddSingleton<IEmailLoader, CsvFileLoader>();
            break;
        case LoaderType.Txt:
            builder.Services.AddSingleton<IEmailLoader, TxtFileLoader>();
            break;
        case LoaderType.MySql:
            builder.Services.AddSingleton<IEmailLoader, MySqlEmailLoader>();
            break;
        case LoaderType.Custom:
            builder.Services.AddSingleton<IEmailLoader, CustomEmailLoader>();
            break;
        default:
            Log.ForContext<Program>().Error("invalid LoaderType");
            throw new ArgumentOutOfRangeException();
    }
    builder.Services.AddHostedService<EmailSender>();
    builder.Services.AddSerilog();

    var app = builder.Build();
    await app.StartAsync();
}
catch (Exception e)
{
    Log.ForContext<Program>().Fatal(e, "application cannot be loaded");
}
finally
{
    await Log.CloseAndFlushAsync();
}