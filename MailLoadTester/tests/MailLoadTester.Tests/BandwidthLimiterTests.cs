using Xunit;

namespace MailLoadTester.Tests;

public sealed class BandwidthLimiterTests
{
    [Fact]
    public async Task LargePayload_DoesNotWaitForever_WhenPayloadExceedsOneSecondBucket()
    {
        var limiter = new MailLoadTester.BandwidthLimiter(8);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));

        await limiter.ThrottleAsync(2048, cts.Token);
    }
}
