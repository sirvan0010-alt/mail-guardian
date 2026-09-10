using MailSenderEngine.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailSenderEngine.Sending;

public sealed class BatchMailSender
{
    private readonly ISmtpSessionFactory _sessionFactory;
    private readonly Configuration.SmtpOptions _options;

    public BatchMailSender(
        ISmtpSessionFactory sessionFactory,
        IOptions<Configuration.SmtpOptions> options)
    {
        _sessionFactory = sessionFactory;
        _options = options.Value;
    }

    public async Task SendAsync(
        IReadOnlyCollection<MailMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
            return;

        await using var session = _sessionFactory.Create();
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await session.AuthenticateAsync(cancellationToken).ConfigureAwait(false);

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await session.SendAsync(BuildMimeMessage(message), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private MimeMessage BuildMimeMessage(MailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(message.From ?? _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };
        return mime;
    }
}
