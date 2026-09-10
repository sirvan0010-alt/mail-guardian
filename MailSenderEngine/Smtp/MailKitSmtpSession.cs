using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MailSenderEngine.Configuration;

namespace MailSenderEngine.Smtp;

public sealed class MailKitSmtpSession(IOptions<SmtpOptions> options) : ISmtpSession
{
    private readonly SmtpOptions _options = options.Value;
    private readonly MailKit.Net.Smtp.SmtpClient _client = new();

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(
            _options.Host,
            _options.Port,
            SecureSocketOptions.StartTls,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await _client.AuthenticateAsync(
            _options.User,
            _options.Password,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(true).ConfigureAwait(false);
            }
        }
        finally
        {
            _client.Dispose();
        }
    }
}
