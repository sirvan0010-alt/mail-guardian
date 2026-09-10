using MailSenderEngine.Configuration;
using MailSenderEngine.Sending;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

namespace MailSenderEngine.Tests;

public sealed class BatchMailSenderTests
{
    [Fact]
    public async Task SendAsync_SendsAllMessagesThroughOnePersistentSession()
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
            FromAddress = "sender@example.test"
        });

        var sender = new BatchMailSender(factory.Object, options);
        var messages = Enumerable.Range(1, 10)
            .Select(i => new MailMessage($"recipient{i}@example.test", $"Subject {i}", $"Body {i}"))
            .ToArray();

        await sender.SendAsync(messages, CancellationToken.None);

        factory.Verify(x => x.Create(), Times.Once);
        session.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
        session.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_DoesNotCreateSessionForEmptyBatch()
    {
        var factory = new Mock<ISmtpSessionFactory>();
        var options = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            User = "user@example.test",
            Password = "not-a-real-password",
            FromAddress = "sender@example.test"
        });

        var sender = new BatchMailSender(factory.Object, options);

        await sender.SendAsync(Array.Empty<MailMessage>(), CancellationToken.None);

        factory.Verify(x => x.Create(), Times.Never);
    }

    [Fact]
    public async Task SendAsync_StopsBeforeNextMessageWhenCancelled()
    {
        var session = new Mock<ISmtpSession>();
        session.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISmtpSessionFactory>();
        factory.Setup(x => x.Create()).Returns(session.Object);

        var options = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            User = "user@example.test",
            Password = "not-a-real-password",
            FromAddress = "sender@example.test"
        });

        using var cancellation = new CancellationTokenSource();
        var sender = new BatchMailSender(factory.Object, options);
        var messages = new[]
        {
            new MailMessage("one@example.test", "One", "One"),
            new MailMessage("two@example.test", "Two", "Two")
        };

        session.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sender.SendAsync(messages, cancellation.Token));
        session.Verify(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
