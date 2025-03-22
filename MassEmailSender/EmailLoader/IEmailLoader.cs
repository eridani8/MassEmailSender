namespace MassEmailSender.EmailLoader;

public interface IEmailLoader
{
    Task<List<string>> LoadEmails();
}