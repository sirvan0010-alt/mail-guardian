using MailSenderEngine.Configuration;
using MailSenderEngine.Sending;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables("MAILSENDER_");

builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ISmtpSessionFactory, SmtpSessionFactory>();
builder.Services.AddTransient<SingleMailSender>();

using var host = builder.Build();

await host.RunAsync();
