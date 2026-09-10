namespace MailSenderEngine.Retry;

public interface IRetryDelayProvider
{
    TimeSpan GetDelay(int attempt);
}
