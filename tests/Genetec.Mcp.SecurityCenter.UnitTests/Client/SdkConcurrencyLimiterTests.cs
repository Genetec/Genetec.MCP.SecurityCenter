using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SdkConcurrencyLimiterTests
{
    [Fact]
    public async Task SameUser_WaitsUntilPermitReleased_OtherUserRunsIndependently()
    {
        using var limiter = CreateLimiter();
        using var first = await limiter.AcquireAsync("a", CancellationToken.None);
        var pending = limiter.AcquireAsync("a", CancellationToken.None).AsTask();
        Assert.False(pending.IsCompleted);
        using var other = await limiter.AcquireAsync("b", CancellationToken.None);
        Assert.True(other.IsAcquired);
        first.Dispose();
        using var next = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(next.IsAcquired);
    }

    [Fact]
    public async Task QueueCancellation_DoesNotConsumePermit()
    {
        using var limiter = CreateLimiter();
        using var first = await limiter.AcquireAsync("a", CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var pending = limiter.AcquireAsync("a", cancel.Token).AsTask();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        first.Dispose();
        using var next = await limiter.AcquireAsync("a", CancellationToken.None);
        Assert.True(next.IsAcquired);
    }

    [Fact]
    public async Task WaitTimeout_DoesNotConsumePermit()
    {
        using var limiter = CreateLimiter(waitSeconds: 1);
        using var first = await limiter.AcquireAsync("a", CancellationToken.None);
        var pending = limiter.AcquireAsync("a", CancellationToken.None).AsTask();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pending.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Contains("wait limit", error.Message, StringComparison.OrdinalIgnoreCase);
        first.Dispose();
        using var next = await limiter.AcquireAsync("a", CancellationToken.None);
        Assert.True(next.IsAcquired);
    }

    [Fact]
    public async Task FullQueue_RejectsAndAllAdmittedRequestsComplete()
    {
        using var limiter = CreateLimiter();
        using var first = await limiter.AcquireAsync("a", CancellationToken.None);
        var queued = Enumerable.Range(0, 50).Select(_ => limiter.AcquireAsync("a", CancellationToken.None).AsTask()).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => limiter.AcquireAsync("a", CancellationToken.None).AsTask());
        first.Dispose();
        foreach (var pending in queued)
        {
            using var acquired = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(acquired.IsAcquired);
        }
    }

    private static SdkConcurrencyLimiter CreateLimiter(int waitSeconds = 30) => new(Options.Create(new RateLimitingOptions
    {
        McpMaxConcurrentPerUser = 1, McpConcurrencyWaitSeconds = waitSeconds
    }));
}
