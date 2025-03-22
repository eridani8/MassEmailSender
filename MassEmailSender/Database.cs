using LiteDB;
using Microsoft.Extensions.Options;

namespace MassEmailSender;

public record SentError
{
    public required ObjectId Id { get; init; }
    public required string Email { get; init; }
    public DateTime DateTime { get; init; } = DateTime.Now;
}
public record SentEmail
{
    public required ObjectId Id { get; init; }
    public required string Email { get; init; }
    public List<string> Subjects { get; set; } = [];
}
public class Database
{
    public ILiteCollection<SentEmail> Emails { get; }
    public ILiteCollection<SentError> ErrorEmails { get; }
    public Database(IOptions<AppSettings> settings)
    {
        LiteDatabase db = new(settings.Value.DbPath);
        Emails = db.GetCollection<SentEmail>("emails");
        ErrorEmails = db.GetCollection<SentError>("error_emails");
    }
}