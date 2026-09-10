using MailSenderEngine.Configuration;
using Microsoft.Extensions.Options;

namespace MailSenderEngine.Smtp;

public interface ISmtpSessionFactory
{
    ISmtpSession Create();
}

public sealed class SmtpSessionFactory(IOptions<SmtpOptions> options) : ISmtpSessionFactory
{
    private readonly IOptions<SmtpOptions> _options = options;

    public ISmtpSession Create() => new MailKitSmtpSession(_options);
}
