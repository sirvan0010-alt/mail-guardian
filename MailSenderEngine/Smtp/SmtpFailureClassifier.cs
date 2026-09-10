using System.IO;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailSenderEngine.Smtp;

public static class SmtpFailureClassifier
{
    public static SmtpFailure Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return new SmtpFailure(SmtpFailureKind.Cancellation);

        if (exception is AuthenticationException)
        {
            var command = FindInner<SmtpCommandException>(exception);
            return command is null
                ? new SmtpFailure(SmtpFailureKind.Authentication)
                : ClassifyStatus(command.StatusCode, SmtpFailureKind.Authentication);
        }

        if (exception is SmtpCommandException commandException)
            return ClassifyStatus(commandException.StatusCode);

        if (exception is SmtpProtocolException ||
            exception is ServiceNotConnectedException ||
            exception is SocketException ||
            exception is IOException ||
            exception is TimeoutException)
        {
            return new SmtpFailure(SmtpFailureKind.Connection);
        }

        return new SmtpFailure(SmtpFailureKind.Unknown);
    }

    private static SmtpFailure ClassifyStatus(
        SmtpStatusCode statusCode,
        SmtpFailureKind authenticationKind = SmtpFailureKind.Unknown)
    {
        var numeric = (int)statusCode;

        if (numeric is >= 400 and < 500)
            return new SmtpFailure(SmtpFailureKind.Temporary, numeric);

        if (numeric >= 500)
        {
            if (authenticationKind != SmtpFailureKind.Unknown)
                return new SmtpFailure(authenticationKind, numeric);

            return new SmtpFailure(SmtpFailureKind.Permanent, numeric);
        }

        return new SmtpFailure(authenticationKind, numeric);
    }

    private static T? FindInner<T>(Exception exception) where T : Exception
    {
        for (var current = exception.InnerException;
             current is not null;
             current = current.InnerException)
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}
