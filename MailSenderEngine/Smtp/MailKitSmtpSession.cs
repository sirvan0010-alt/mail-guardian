using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailSenderEngine.Configuration;

namespace MailSenderEngine.Smtp;

public sealed class MailKitSmtpSession(
    IOptions<SmtpOptions> options,
    ILogger<MailKitSmtpSession> logger) : ISmtpSession
{
    private readonly SmtpOptions _options = options.Value;
    private readonly ILogger<MailKitSmtpSession> _logger = logger;
    private readonly MailKit.Net.Smtp.SmtpClient _client = new();

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SMTP connect {Host}:{Port}", _options.Host, _options.Port);
            await _client.ConnectAsync(
                _options.Host,
                _options.Port,
                SecureSocketOptions.StartTls,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SMTP connect cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connect failed to {Host}:{Port}", _options.Host, _options.Port);
            throw;
        }
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SMTP authenticate {User}", _options.User);
            await _client.AuthenticateAsync(
                _options.User,
                _options.Password,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SMTP authentication cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP authentication failed for {User}", _options.User);
            throw;
        }
    }

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            _logger.LogInformation("SMTP send to {Recipients}; subject={Subject}",
                string.Join(",", message.To.Mailboxes.Select(x => x.Address)),
                message.Subject);
            await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SMTP send cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed; subject={Subject}", message.Subject);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                _logger.LogInformation("SMTP disconnect {Host}:{Port}", _options.Host, _options.Port);
                await _client.DisconnectAsync(true).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP disconnect failed from {Host}:{Port}", _options.Host, _options.Port);
        }
        finally
        {
            _client.Dispose();
        }
    }
}
