using Microsoft.Extensions.Options;
using Serilog;

namespace MassEmailSender.EmailLoader;

public class TxtFileLoader(IOptions<AppSettings> settings) : IEmailLoader
{
    public async Task<List<string>> LoadEmails()
    {
        List<string> emails = [];
        using var sr = new StreamReader(settings.Value.TxtEmailsPath);
        while (await sr.ReadLineAsync() is { } line)
        {
            try
            {
                if (line.ValidateEmail())
                {
                    emails.Add(line);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "LoadEmails.while: {line}", line);
            }
        }
        return emails;
    }
}