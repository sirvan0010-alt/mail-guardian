using MailSenderEngine.Smtp;
using Microsoft.Extensions.Logging;

namespace MailSenderEngine.Retry;

public sealed class SmtpRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly IRetryDelayProvider _delayProvider;
    private readonly ILogger<SmtpRetryPolicy> _logger;

    public SmtpRetryPolicy(
        RetryOptions options,
        IRetryDelayProvider delayProvider,
        ILogger<SmtpRetryPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await operation(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                var delay = _delayProvider.GetDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Transient SMTP failure. Retry attempt {Attempt}/{MaxAttempts} after {Delay}.",
                    attempt,
                    _options.MaxAttempts,
                    delay);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldRetry(Exception exception, int attempt)
    {
        if (attempt >= _options.MaxAttempts)
            return false;

        var failure = SmtpFailureClassifier.Classify(exception);
        return failure.Kind == SmtpFailureKind.Temporary ||
               failure.Kind == SmtpFailureKind.Connection;
    }
}
