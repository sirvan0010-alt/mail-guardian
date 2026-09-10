using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ISmtpSessionFactory, MailKitSmtpSessionFactory>();

using var host = builder.Build();

await host.RunAsync();

public sealed class SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public interface ISmtpSessionFactory
{
    ISmtpSession Create();
}

public interface ISmtpSession : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task AuthenticateAsync(CancellationToken cancellationToken);
}

public sealed class MailKitSmtpSessionFactory(IOptions<SmtpOptions> options) : ISmtpSessionFactory
{
    public ISmtpSession Create() => new MailKitSmtpSession(options.Value);
}

public sealed class MailKitSmtpSession(SmtpOptions options) : ISmtpSession
{
    private readonly MailKit.Net.Smtp.SmtpClient _client = new();

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(
            options.Host,
            options.Port,
            MailKit.Security.SecureSocketOptions.StartTls,
            cancellationToken);
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await _client.AuthenticateAsync(options.User, options.Password, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(true);
        }

        _client.Dispose();
    }
}
