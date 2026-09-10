namespace MailSenderEngine.Sending;

public enum MailStatus
{
    Queued,
    Sending,
    Accepted,
    RetryWait,
    Failed,
    Bounced
}
