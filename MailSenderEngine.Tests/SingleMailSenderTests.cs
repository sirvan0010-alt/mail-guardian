using MailSenderEngine.Configuration;
using MailSenderEngine.Sending;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

namespace MailSenderEngine.Tests;

public sealed class SingleMailSenderTests
{
    [Fact]
    public async Task SendAsync_CallsConnectAuthenticateAndSendInOrder()
    {
        var sequence = new MockSequence();
        var session = new Mock<ISmtpSession>();
        session.InSequence(sequence)
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        session.InSequence(sequence)
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        session.InSequence(sequence)
            .Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISmtpSessionFactory>();
        factory.Setup(x => x.Create()).Returns(session.Object);

        var options = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            User = "user@example.test",
            Password = "not-a-real-password",
            FromAddress = "sender@example.test",
            FromDisplayName = "Test Sender"
        });

        var sender = new SingleMailSender(factory.Object, options);
        using var cancellation = new CancellationTokenSource();
        var message = new MailMessage("recipient@example.test", "Test subject", "Hello");

        await sender.SendAsync(message, cancellation.Token);

        session.Verify(x => x.ConnectAsync(cancellation.Token), Times.Once);
        session.Verify(x => x.AuthenticateAsync(cancellation.Token), Times.Once);
        session.Verify(x => x.SendAsync(
            It.Is<MimeMessage>(m =>
                m.From.Mailboxes.Single().Address == "sender@example.test" &&
                m.From.Mailboxes.Single().Name == "Test Sender" &&
                m.To.Mailboxes.Single().Address == "recipient@example.test" &&
                m.Subject == "Test subject" &&
                ((TextPart)m.Body).Text == "Hello"),
            cancellation.Token), Times.Once);
        session.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_UsesMessageFromWhenProvided()
    {
        var session = new Mock<ISmtpSession>();
        session.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var factory = new Mock<ISmtpSessionFactory>();
        factory.Setup(x => x.Create()).Returns(session.Object);

        var options = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            User = "user@example.test",
            Password = "not-a-real-password",
            FromAddress = "configured@example.test"
        });

        var sender = new SingleMailSender(factory.Object, options);
        var message = new MailMessage(
            "recipient@example.test",
            "Test subject",
            "Hello",
            "override@example.test");

        await sender.SendAsync(message, CancellationToken.None);

        session.Verify(x => x.SendAsync(
            It.Is<MimeMessage>(m => m.From.Mailboxes.Single().Address == "override@example.test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
