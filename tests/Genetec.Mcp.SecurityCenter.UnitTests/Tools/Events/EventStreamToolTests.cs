using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.Events;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Events;

public class EventStreamToolTests
{
    // ── DataAnnotations ─────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Request_OutOfRangeTimeout_FailsValidation(int timeout)
    {
        var request = new EventStreamRequest(timeout);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EventStreamRequest.TimeoutSeconds)));
    }

    [Theory]
    [InlineData("{\"EventType\":\"DoorRexOn\",\"SourceGuid\":\"11111111-2222-3333-4444-555555555555\"}", true, false)]
    [InlineData("{\"EventType\":\"DoorRexOn\",\"SourceGuid\":\"11111111-2222-3333-4444-555555555555\"}", true, true)]
    [InlineData("{\"EventType\":\"DoorRexOn\",", false, false)]
    [InlineData("{\"EventType\":\"DoorRexOn\",", false, true)]
    [InlineData("KeepAlive", false, false)]
    [InlineData("KeepAlive", false, true)]
    public async Task Stream_FinalFrameWithoutClosingBoundary_ReturnsOnlyCompleteEvents(string payload, bool expectedEvent, bool waitForTimeout)
    {
        var manager = TestHelpers.FakeManager(new StreamHandler(payload, waitForTimeout));
        var result = Assert.IsType<string>(await EventStreamTool.Execute(manager, new EventStreamRequest(1)));
        using var document = JsonDocument.Parse(result);
        var events = document.RootElement;
        Assert.Equal(expectedEvent ? 1 : 0, events.GetArrayLength());
        if (expectedEvent)
        {
            Assert.Equal("DoorRexOn", events[0].GetProperty("EventType").GetString());
            Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), events[0].GetProperty("SourceGuid").GetGuid());
        }
    }

    private sealed class StreamHandler(string payload, bool waitForTimeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new EventStream(
                    System.Text.Encoding.UTF8.GetBytes($"--GENETECBOUNDARY\nContent-Type: text/json\n\n{payload}\n"), waitForTimeout))
            });
    }

    private sealed class EventStream(byte[] bytes, bool waitForTimeout) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == Length && waitForTimeout)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
