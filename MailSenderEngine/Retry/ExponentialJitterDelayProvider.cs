using System.Security.Cryptography;

namespace MailSenderEngine.Retry;

public sealed class ExponentialJitterDelayProvider : IRetryDelayProvider
{
    private readonly RetryOptions _options;

    public ExponentialJitterDelayProvider(RetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxAttempts));
        if (options.InitialDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.InitialDelay));
        if (options.MaxDelay < options.InitialDelay)
            throw new ArgumentOutOfRangeException(nameof(options.MaxDelay));
        if (options.JitterRatio < 0 || options.JitterRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(options.JitterRatio));

        _options = options;
    }

    public TimeSpan GetDelay(int attempt)
    {
        if (attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(attempt));

        var exponent = Math.Min(attempt - 1, 30);
        var baseMilliseconds = _options.InitialDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var cappedMilliseconds = Math.Min(baseMilliseconds, _options.MaxDelay.TotalMilliseconds);

        if (cappedMilliseconds <= 0 || _options.JitterRatio == 0)
            return TimeSpan.FromMilliseconds(cappedMilliseconds);

        var jitter = (RandomNumberGenerator.GetInt32(-1_000_000, 1_000_001) / 1_000_000d)
                     * _options.JitterRatio;
        var result = cappedMilliseconds * (1d + jitter);
        return TimeSpan.FromMilliseconds(Math.Max(0, Math.Min(result, _options.MaxDelay.TotalMilliseconds)));
    }
}
