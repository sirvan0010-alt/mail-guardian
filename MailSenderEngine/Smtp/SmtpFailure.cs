namespace MailSenderEngine.Smtp;

public enum SmtpFailureKind
{
    None,
    Temporary,
    Permanent,
    Connection,
    Authentication,
    Cancellation,
    Unknown
}

public sealed record SmtpFailure(SmtpFailureKind Kind, int? StatusCode = null);
