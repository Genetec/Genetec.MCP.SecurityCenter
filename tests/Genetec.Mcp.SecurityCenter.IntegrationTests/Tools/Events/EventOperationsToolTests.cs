using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Events;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Events;

public class EventOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task ListSubscriptions_ReturnsJsonArray()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.ListSubscriptions));

        var str = Assert.IsType<string>(result);
        Assert.StartsWith("[", str);
        Assert.EndsWith("]", str);
    }

    [Fact]
    public async Task Subscribe_Unsubscribe_RoundTrip()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var doorId = await GetOrCreateDoorAsync();
        Assert.True(doorId is not null, "Could not find or create a Door entity.");

        // Subscribe
        var subResult = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.Subscribe,
                EntityId: doorId,
                EventTypes: [ScEventType.DoorOpen]));
        Assert.NotNull(subResult);

        // Verify subscription appears in the list
        var listResult = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.ListSubscriptions));
        var listStr = Assert.IsType<string>(listResult);
        Assert.Contains(doorId!.Value.ToString(), listStr, StringComparison.OrdinalIgnoreCase);

        // Unsubscribe
        var unsubResult = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.Unsubscribe,
                EntityId: doorId,
                EventTypes: [ScEventType.DoorOpen]));
        Assert.NotNull(unsubResult);

        // Verify subscription is gone
        var afterResult = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.ListSubscriptions));
        var afterStr = Assert.IsType<string>(afterResult);
        Assert.DoesNotContain(doorId!.Value.ToString(), afterStr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RaiseEvent_DoorRexOn_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var doorId = await GetOrCreateDoorAsync();
        Assert.True(doorId is not null, "Could not find or create a Door entity.");

        var result = await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.RaiseEvent,
                EntityId: doorId,
                EventType: ScRaisableEventType.DoorRexOn));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SubscribeMultipleEventTypes_AllAppearsInSubscriptions()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var doorId = await GetOrCreateDoorAsync();
        Assert.True(doorId is not null, "Could not find or create a Door entity.");

        var eventTypes = new List<ScEventType> { ScEventType.DoorOpen, ScEventType.DoorClose, ScEventType.DoorLock };

        // Subscribe to multiple event types at once
        await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.Subscribe,
                EntityId: doorId,
                EventTypes: eventTypes));

        var listStr = Assert.IsType<string>(await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.ListSubscriptions)));

        Assert.Contains(doorId!.Value.ToString(), listStr, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        await EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.Unsubscribe,
                EntityId: doorId,
                EventTypes: eventTypes));
    }

    [Fact]
    public async Task SubscribeAndUnsubscribe_AllEventTypeValues_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var doorId = await GetOrCreateDoorAsync();
        Assert.True(doorId is not null, "Could not find or create a Door entity.");

        foreach (var eventTypes in Enum.GetValues<ScEventType>().Chunk(25))
        {
            var eventTypeList = eventTypes.ToList();
            var subscribeException = await Record.ExceptionAsync(() => EventOperationsTool.Execute(sc.Manager,
                new EventOperationRequest(EventOperationAction.Subscribe,
                    EntityId: doorId,
                    EventTypes: eventTypeList)));
            // The SDK can register earlier entries before rejecting a later
            // event name. Unsubscribe before asserting to clean up that prefix.
            var unsubscribeException = await Record.ExceptionAsync(() => EventOperationsTool.Execute(sc.Manager,
                new EventOperationRequest(EventOperationAction.Unsubscribe,
                    EntityId: doorId,
                    EventTypes: eventTypeList)));
            Assert.Null(subscribeException);
            Assert.Null(unsubscribeException);
        }
    }

    [Fact]
    public async Task RaiseEvent_AllRaisableEventTypeValues_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var exceptions = new List<Exception?>();
        foreach (var eventType in Enum.GetValues<ScRaisableEventType>())
        {
            exceptions.Add(await Record.ExceptionAsync(() => EventOperationsTool.Execute(sc.Manager,
                new EventOperationRequest(EventOperationAction.RaiseEvent,
                    EntityId: Guid.NewGuid(),
                    EventType: eventType))));
        }

        Assert.All(exceptions, exception =>
            Assert.True(exception is null or not ArgumentException, exception?.ToString()));
    }

    [Fact]
    public async Task RaiseCustomEvent_UnknownEvent_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => EventOperationsTool.Execute(sc.Manager,
            new EventOperationRequest(EventOperationAction.RaiseCustomEvent,
                EntityId: Guid.NewGuid(), CustomEventId: 999999, Message: "MCP integration test")));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    private async Task<Guid?> GetOrCreateDoorAsync()
    {
        var existing = await GetFirstEntityGuid("Door");
        if (existing is not null) return Guid.TryParse(existing, out var g) ? g : null;

        var name = $"MCP_TestDoor_{Guid.NewGuid():N}";
        var created = (Genetec.Mcp.SecurityCenter.Client.EntityCreateResult)
            await DoorCreateTool.Execute(sc.Manager, new DoorCreateRequest([new DoorItem(name)]));
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<string?> GetFirstEntityGuid(string entityType)
    {
        try
        {
            var json = await sc.Manager.GetClient().ListEntities(entityType, 1, 1, ["Name"]);
            if (json is null) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Body", out var body) || body.ValueKind != JsonValueKind.Array || body.GetArrayLength() == 0)
                return null;

            var first = body[0];
            if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0)
                return first[0].GetString();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("Guid", out var g))
                return g.GetString();

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
