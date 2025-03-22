using Microsoft.Extensions.Options;
using Serilog;

namespace MassEmailSender.EmailLoader;

public class CsvFileLoader(IOptions<AppSettings> settings) : IEmailLoader
{
    public async Task<List<string>> LoadEmails()
    {
        List<string> emails = [];
        using var sr = new StreamReader(settings.Value.CsvEmailsPath);
        while (await sr.ReadLineAsync() is { } line)
        {
            try
            {
                line = line.Remove(0, 1);
                line = line.Remove(line.Length - 1, 1);
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