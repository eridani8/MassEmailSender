using MassEmailSender.EmailLoader;

namespace MassEmailSender;

// ReSharper disable once ClassNeverInstantiated.Global
public record AppSettings
{
    public required string DbPath { get; init; }
    public required int MessagesLimit { get; init; }
    
    public required string MySqlConnectionString { get; init; }
    public required string TxtEmailsPath { get; init; }
    public required string CsvEmailsPath { get; init; }
    
    
    public required LoaderType LoaderType { get; init; }
    public required bool CheckEmailSubjects { get; init; }
    public required bool ShuffleEmails { get; init; }
    
    public required string SmtpHost { get; init; }
    public required int SmtpPort { get; init; }
    
    public required string CredentialName { get; init; }
    public required string CredentialPassword { get; init; }
    
    public required string CrystalName{ get; init; }
    public required string CrystalEmail{ get; init; }
    
    public required string EmailSubject { get; init; }
    public required string BodyFilePath { get; init; }
    public ColorSettings ColorSettings { get; init; } = new();
}

public record ColorSettings
{
    public string ValueColor { get; init; } = "plum2";
    public string ErrorColor { get; init; } = "red";
    public string MessageColor { get; init; } = "teal";
}