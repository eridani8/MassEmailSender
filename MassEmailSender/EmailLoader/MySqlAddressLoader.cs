using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace MassEmailSender.EmailLoader;

public class MySqlEmailLoader(IOptions<AppSettings> settings) : IEmailLoader
{
    public async Task<List<string>> LoadEmails()
    {
        await using var connection = new MySqlConnection(settings.Value.MySqlConnectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand()
        {
            Connection = connection
        };
        cmd.CommandText = "SELECT `email` FROM `accounts`";
        DbDataReader reader = await cmd.ExecuteReaderAsync();
        DataTable dataTable = new();
        dataTable.Load(reader);
        await connection.CloseAsync();
        List<string> emails = [];
        if (dataTable.Rows.Count == 0) return emails;
        foreach (DataRow row in dataTable.Rows)
        {
            var email = row["email"].ToString();
            if (string.IsNullOrEmpty(email) || emails.Contains(email)) continue;
            if (email.ValidateEmail())
            {
                emails.Add(email);
            }
        }
        return emails;
    }
}