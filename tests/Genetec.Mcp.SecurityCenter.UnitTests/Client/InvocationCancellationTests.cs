using System.Net;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class InvocationCancellationTests
{
    [Fact]
    public async Task CancelledInvocation_IssuesNoSdkRequest()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        using var cancel = new CancellationTokenSource();
        var client = new SecurityCenterRestClient(Session(), http, cancellationToken: cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetEntity("id"));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task MultipleSdkRequests_EachReceivesItsOwnHttpTimeout()
    {
        var handler = new CountingHandler { RequestDelay = TimeSpan.FromMilliseconds(600) };
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(1), http);
        await client.GetEntity("first");
        await client.GetEntity("second");
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task IdleTimeDoesNotExpireLaterRequest()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(1), http);
        await client.GetEntity("first");
        await Task.Delay(1100);
        await client.GetEntity("second");
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task CallerCancellation_ReachesHttp_AndClientAcceptsLaterRequest()
    {
        var handler = new CountingHandler { BlockedRequest = 1 };
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(), http);
        using var cancel = new CancellationTokenSource();
        var pending = client.SendAsync("entity/first", "GET", ct: cancel.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await client.GetEntity("second");
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task CallerCancellation_ReachesConcurrencyWait_WithoutIssuingHttp()
    {
        using var limiter = new SdkConcurrencyLimiter(Options.Create(new RateLimitingOptions
        {
            McpMaxConcurrentPerUser = 1, McpConcurrencyWaitSeconds = 30
        }));
        using var occupied = await limiter.AcquireAsync("user", CancellationToken.None);
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(), http, sdkLimiter: limiter, userId: "user");
        using var cancel = new CancellationTokenSource();
        var pending = client.SendAsync("entity/first", "GET", ct: cancel.Token);
        Assert.False(pending.IsCompleted);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, handler.Count);
        occupied.Dispose();
        await client.GetEntity("second");
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task HttpTimeout_StopsQueuedBatchWork_PreservesCompletedResults_AndFreshClientRuns()
    {
        using var limiter = new SdkConcurrencyLimiter(Options.Create(new RateLimitingOptions
        {
            McpMaxConcurrentPerUser = 1, McpConcurrencyWaitSeconds = 30
        }));
        var handler = new CountingHandler { BlockedRequest = 2 };
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(1), http, sdkLimiter: limiter,
            userId: "user");
        var batch = await client.ExecuteEntityMethodBatch("method", [(Guid.NewGuid(), null), (Guid.NewGuid(), null), (Guid.NewGuid(), null)])
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(2, batch.FailureCount);
        var results = batch.Results;
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.False(results[2].Success);
        Assert.Equal(2, handler.Count);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetEntity("stopped"));
        Assert.Equal(2, handler.Count);

        using var freshHttp = new HttpClient(handler, disposeHandler: false);
        var fresh = new SecurityCenterRestClient(Session(1), freshHttp, sdkLimiter: limiter,
            userId: "user");
        await fresh.GetEntity("fresh");
        Assert.Equal(3, handler.Count);
    }

    [Fact]
    public async Task HttpTimeout_StopsFurtherRequests_AndFreshClientRuns()
    {
        var handler = new CountingHandler { BlockedRequest = 1 };
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(1), http);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetEntity("first"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetEntity("stopped"));
        Assert.Equal(1, handler.Count);

        using var freshHttp = new HttpClient(handler, disposeHandler: false);
        var fresh = new SecurityCenterRestClient(Session(1), freshHttp);
        await fresh.GetEntity("fresh");
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task CancelledBatch_PreservesCompletedItemAndMarksRemainingFailures()
    {
        using var cancel = new CancellationTokenSource();
        var handler = new CountingHandler(cancel);
        using var http = new HttpClient(handler);
        var client = new SecurityCenterRestClient(Session(), http, cancellationToken: cancel.Token);
        var result = await client.ExecuteEntityMethodBatch("method", [(Guid.NewGuid(), null), (Guid.NewGuid(), null)]);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(1, handler.Count);
    }

    private static SecurityCenterSession Session(int seconds = 30) => new(new ScConnectionContext
    {
        Host = "localhost", Port = 4590, User = "test", Password = "test", InvokeTimeoutSec = seconds
    });

    private sealed class CountingHandler(CancellationTokenSource? cancel = null) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public int? BlockedRequest { get; init; }
        public TimeSpan RequestDelay { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            if (Count == BlockedRequest)
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            if (RequestDelay > TimeSpan.Zero)
                await Task.Delay(RequestDelay, cancellationToken);
            // Cancel after delivering the first response, so its result remains known.
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            if (cancel is not null) response.Content = new CancelAfterReadContent(cancel);
            return response;
        }
    }

    private sealed class CancelAfterReadContent(CancellationTokenSource cancel) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            await stream.WriteAsync("{}"u8.ToArray());
            cancel.Cancel();
        }
        protected override bool TryComputeLength(out long length) { length = 2; return true; }
    }
}
