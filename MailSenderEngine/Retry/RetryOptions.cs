namespace MailSenderEngine.Retry;

public sealed class RetryOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(2);
    public double JitterRatio { get; init; } = 0.20;
}
