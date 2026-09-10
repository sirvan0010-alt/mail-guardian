using MimeKit;

namespace MailSenderEngine.Smtp;

public interface ISmtpSession : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task AuthenticateAsync(CancellationToken cancellationToken);
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}
