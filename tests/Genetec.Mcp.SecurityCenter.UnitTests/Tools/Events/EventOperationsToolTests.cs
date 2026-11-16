using Genetec.Mcp.SecurityCenter.Tools.Events;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Events;

public class EventOperationsToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(EventOperationAction.Subscribe)]
    [InlineData(EventOperationAction.Unsubscribe)]
    [InlineData(EventOperationAction.RaiseEvent)]
    [InlineData(EventOperationAction.RaiseCustomEvent)]
    public async Task NullEntityId_ThrowsArgumentException(EventOperationAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EventOperationsTool.Execute(TestHelpers.FakeManager(),
                new EventOperationRequest(action, EntityId: null,
                    EventTypes: [ScEventType.DoorOpen],
                    EventType: ScRaisableEventType.DoorRexOn,
                    CustomEventId: 1)));

        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(EventOperationAction.Subscribe)]
    [InlineData(EventOperationAction.Unsubscribe)]
    public async Task NullEventTypes_ThrowsArgumentException(EventOperationAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EventOperationsTool.Execute(TestHelpers.FakeManager(),
                new EventOperationRequest(action, EntityId: Guid.NewGuid(),EventTypes: null)));

        Assert.Contains("event_types", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RaiseEvent_NullEventType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EventOperationsTool.Execute(TestHelpers.FakeManager(),
                new EventOperationRequest(EventOperationAction.RaiseEvent,
                    EntityId: Guid.NewGuid(),EventType: null)));

        Assert.Contains("event_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RaiseCustomEvent_NullCustomEventId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EventOperationsTool.Execute(TestHelpers.FakeManager(),
                new EventOperationRequest(EventOperationAction.RaiseCustomEvent,
                    EntityId: Guid.NewGuid(),CustomEventId: null)));

        Assert.Contains("custom_event_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
