using MailSenderEngine.Smtp;
using MimeKit;

namespace MailSenderEngine.Sending;

public sealed class SingleMailSender(ISmtpSessionFactory sessionFactory)
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mimeMessage = BuildMimeMessage(message);

        await using var session = sessionFactory.Create();
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await session.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        await session.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
    }

    private static MimeMessage BuildMimeMessage(MailMessage message)
    {
        var mime = new MimeMessage();
        mime.To.Add(MailboxAddress.Parse(message.To));

        if (!string.IsNullOrWhiteSpace(message.From))
        {
            mime.From.Add(MailboxAddress.Parse(message.From));
        }

        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain")
        {
            Text = message.TextBody
        };

        return mime;
    }
}
