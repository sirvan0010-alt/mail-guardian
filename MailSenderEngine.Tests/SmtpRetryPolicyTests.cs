using MailKit.Net.Smtp;
using MailSenderEngine.Retry;
using MailSenderEngine.Smtp;
using Microsoft.Extensions.Logging;
using Moq;

namespace MailSenderEngine.Tests;

public sealed class SmtpRetryPolicyTests
{
    [Fact]
    public async Task TemporaryFailure_RetriesUntilSuccess()
    {
        var delays = new FixedDelayProvider();
        var policy = CreatePolicy(maxAttempts: 3, delays);
        var attempts = 0;

        await policy.ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 3)
                throw TemporaryFailure();

            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.Equal(2, delays.Calls);
    }

    [Fact]
    public async Task PermanentFailure_IsNotRetried()
    {
        var delays = new FixedDelayProvider();
        var policy = CreatePolicy(maxAttempts: 3, delays);
        var attempts = 0;

        await Assert.ThrowsAsync<SmtpCommandException>(() => policy.ExecuteAsync(_ =>
        {
            attempts++;
            throw PermanentFailure();
        }, CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Equal(0, delays.Calls);
    }

    [Fact]
    public async Task MaxAttempts_StopsRetries()
    {
        var delays = new FixedDelayProvider();
        var policy = CreatePolicy(maxAttempts: 3, delays);
        var attempts = 0;

        await Assert.ThrowsAsync<SmtpCommandException>(() => policy.ExecuteAsync(_ =>
        {
            attempts++;
            throw TemporaryFailure();
        }, CancellationToken.None));

        Assert.Equal(3, attempts);
        Assert.Equal(2, delays.Calls);
    }

    [Fact]
    public async Task Cancellation_DoesNotRetry()
    {
        var delays = new FixedDelayProvider();
        var policy = CreatePolicy(maxAttempts: 3, delays);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var attempts = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() => policy.ExecuteAsync(_ =>
        {
            attempts++;
            return Task.CompletedTask;
        }, cts.Token));

        Assert.Equal(0, attempts);
        Assert.Equal(0, delays.Calls);
    }

    private static SmtpRetryPolicy CreatePolicy(int maxAttempts, FixedDelayProvider delays)
    {
        var logger = Mock.Of<ILogger<SmtpRetryPolicy>>();
        return new SmtpRetryPolicy(
            new RetryOptions
            {
                MaxAttempts = maxAttempts,
                InitialDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
                JitterRatio = 0
            },
            delays,
            logger);
    }

    private static SmtpCommandException TemporaryFailure() =>
        new(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxBusy,
            "temporary failure");

    private static SmtpCommandException PermanentFailure() =>
        new(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxDoesNotExist,
            "permanent failure");

    private sealed class FixedDelayProvider : IRetryDelayProvider
    {
        public int Calls { get; private set; }

        public TimeSpan GetDelay(int attempt)
        {
            Calls++;
            return TimeSpan.Zero;
        }
    }
}
