using MailSenderEngine.Configuration;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailSenderEngine.Sending;

public sealed class SingleMailSender(
    ISmtpSessionFactory sessionFactory,
    IOptions<SmtpOptions> options)
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var mimeMessage = BuildMimeMessage(message);

        await using var session = sessionFactory.Create();
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await session.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        await session.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
    }

    private MimeMessage BuildMimeMessage(MailMessage message)
    {
        var mime = new MimeMessage();
        var fromAddress = string.IsNullOrWhiteSpace(message.From)
            ? _options.FromAddress
            : message.From;

        mime.From.Add(new MailboxAddress(_options.FromDisplayName, fromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain")
        {
            Text = message.TextBody
        };

        return mime;
    }
}
