using MailSenderEngine.Configuration;
using MailSenderEngine.Sending;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Environment variables use the standard .NET hierarchical form:
// MAILSENDER_Smtp__Host, MAILSENDER_Smtp__Port, MAILSENDER_Smtp__User,
// MAILSENDER_Smtp__Password, MAILSENDER_Smtp__FromAddress.
builder.Configuration.AddEnvironmentVariables("MAILSENDER_");

builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ISmtpSessionFactory, SmtpSessionFactory>();
builder.Services.AddTransient<SingleMailSender>();

using var host = builder.Build();

if (args.Length > 0 && string.Equals(args[0], "--send", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        throw new ArgumentException("Usage: --send <to> <subject> <body>");
    }

    var sender = host.Services.GetRequiredService<SingleMailSender>();
    var message = new MailMessage(args[1], args[2], args[3]);

    await sender.SendAsync(message, CancellationToken.None);
    return;
}

await host.RunAsync();
