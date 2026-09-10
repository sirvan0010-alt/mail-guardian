namespace MailSenderEngine.Sending;

public sealed record MailMessage(
    string To,
    string Subject,
    string TextBody,
    string? From = null);
