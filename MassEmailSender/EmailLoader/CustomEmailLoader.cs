namespace MassEmailSender.EmailLoader;

public class CustomEmailLoader : IEmailLoader
{
    public Task<List<string>> LoadEmails()
    {
        List<string> emails =
        [
            "tsuuzetsu@gmail.com"
        ];
        return Task.FromResult(emails);
    }
}