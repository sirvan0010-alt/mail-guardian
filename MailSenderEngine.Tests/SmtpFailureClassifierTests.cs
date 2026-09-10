using MailKit.Net.Smtp;
using MailKit.Security;
using MailSenderEngine.Smtp;

namespace MailSenderEngine.Tests;

public sealed class SmtpFailureClassifierTests
{
    [Fact]
    public void FourHundredSeries_IsTemporary()
    {
        var exception = new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxBusy,
            "temporary failure");

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(SmtpFailureKind.Temporary, result.Kind);
        Assert.Equal(450, result.StatusCode);
    }

    [Fact]
    public void FiveHundredSeries_IsPermanent()
    {
        var exception = new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            "permanent failure");

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(SmtpFailureKind.Permanent, result.Kind);
        Assert.Equal(550, result.StatusCode);
    }

    [Fact]
    public void AuthenticationException_IsAuthenticationFailure()
    {
        var exception = new AuthenticationException("invalid credentials");

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(SmtpFailureKind.Authentication, result.Kind);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public void ProtocolException_IsConnectionFailure()
    {
        var exception = new SmtpProtocolException("connection dropped");

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(SmtpFailureKind.Connection, result.Kind);
    }

    [Fact]
    public void Cancellation_IsCancellation()
    {
        var result = SmtpFailureClassifier.Classify(new OperationCanceledException());

        Assert.Equal(SmtpFailureKind.Cancellation, result.Kind);
    }
}
