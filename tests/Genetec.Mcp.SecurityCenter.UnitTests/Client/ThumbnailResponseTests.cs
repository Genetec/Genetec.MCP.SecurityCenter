using System.Net;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class ThumbnailResponseTests
{
    [Fact]
    public async Task Buffering_CallerCancellation_DisposesResponse()
    {
        using var cancellation = new CancellationTokenSource();
        var content = new BlockingContent();
        using var http = new HttpClient(new ResponseHandler(content));
        var client = new SecurityCenterRestClient(new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost", User = "test", Password = "test"
        }), http, cancellationToken: cancellation.Token);

        var pending = client.GetThumbnails([Guid.NewGuid()], "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z");
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Buffering_HttpTimeout_DisposesResponse()
    {
        var content = new BlockingContent();
        using var http = new HttpClient(new ResponseHandler(content));
        var client = new SecurityCenterRestClient(new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost", User = "test", Password = "test", InvokeTimeoutSec = 1
        }), http);

        var pending = client.GetThumbnails([Guid.NewGuid()], "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z");
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(content.Disposed);
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new InvalidOperationException("Expected cancellation-aware buffering.");
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Started.SetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class ResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

}
