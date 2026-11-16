using Genetec.Mcp.SecurityCenter.Tools.Events;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Events;

public class EventStreamToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Stream_SubscribedDoorEvent_ReturnsMatchingSourceTypeAndTimestamp()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var created = Assert.IsType<EntityCreateResult>(await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Stream_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var subscription = new EventOperationRequest(EventOperationAction.Subscribe,
            EntityId: created.Guid, EventTypes: [ScEventType.DoorRexOn]);
        try
        {
            await EventOperationsTool.Execute(sc.Manager, subscription);
            var started = DateTime.UtcNow;
            var stream = EventStreamTool.Execute(sc.Manager, new EventStreamRequest(5));
            // Keep generating while the stream opens and collects so a slow
            // handshake cannot cause the only event to precede the connection.
            while (!stream.IsCompleted)
            {
                await Task.WhenAny(stream, Task.Delay(TimeSpan.FromSeconds(1)));
                if (!stream.IsCompleted)
                    await EventOperationsTool.Execute(sc.Manager,
                        new EventOperationRequest(EventOperationAction.RaiseEvent,
                            EntityId: created.Guid, EventType: ScRaisableEventType.DoorRexOn));
            }
            var result = Assert.IsType<string>(await stream);
            using var document = IntegrationTestHelpers.ParseJson(result);
            var events = IntegrationTestHelpers.GetRows(document.RootElement).EnumerateArray()
                .Select(item => IntegrationTestHelpers.GetResult(item)).ToList();
            Assert.Contains(events, item => item.GetProperty("SourceGuid").GetGuid() == created.Guid &&
                item.GetProperty("EventType").GetString() == "DoorRexOn" &&
                item.GetProperty("Timestamp").GetDateTime().ToUniversalTime() >= started.AddSeconds(-1) &&
                item.GetProperty("Timestamp").GetDateTime().ToUniversalTime() <= DateTime.UtcNow.AddSeconds(1));
        }
        finally
        {
            await EventOperationsTool.Execute(sc.Manager,
                subscription with { Action = EventOperationAction.Unsubscribe });
        }
    }
}
