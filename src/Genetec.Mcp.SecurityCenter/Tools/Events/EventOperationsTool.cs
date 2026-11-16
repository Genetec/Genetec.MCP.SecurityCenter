using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Events;

public enum EventOperationAction
{
    ListSubscriptions,
    Subscribe,
    Unsubscribe,
    RaiseEvent,
    RaiseCustomEvent
}

public sealed record EventOperationRequest(
    [property: Description("Operation to perform"), Required] EventOperationAction Action,
    [property: Description("Entity GUID — required for subscribe, unsubscribe, raise_event, raise_custom_event")] Guid? EntityId = null,
    [property: Description("Event types to subscribe or unsubscribe — required for subscribe and unsubscribe"),
     MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)]
    List<ScEventType>? EventTypes = null,
    [Description("Event type to raise — required for raise_event")] ScRaisableEventType? EventType = null,
    [Description("Custom event numeric ID — required for raise_custom_event")] int? CustomEventId = null,
    [Description("Optional message for raise_custom_event")] string? Message = null);

[McpServerToolType]
public class EventOperationsTool
{
    [McpServerTool(
        Name = "event_operations",
        Title = "Event Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage Security Center event subscriptions and raise events. " +
        "Use list_subscriptions to see active subscriptions, subscribe/unsubscribe to monitor entities, " +
        "raise_event to trigger a built-in SC event type, or raise_custom_event to trigger a custom event by its numeric ID. " +
        "Call event_stream after subscribing to collect events.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Event operation request")] EventOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case EventOperationAction.ListSubscriptions:
                return await client.GetEventSubscriptions();

            case EventOperationAction.Subscribe:
                if (request.EntityId is null)
                    throw new ArgumentException("Parameter 'entity_id' is required for action 'subscribe'.");
                if (request.EventTypes is null || request.EventTypes.Count == 0)
                    throw new ArgumentException("Parameter 'event_types' is required for action 'subscribe'.");
                return await client.SubscribeToEvents(request.EntityId!.ToString(), request.EventTypes);

            case EventOperationAction.Unsubscribe:
                if (request.EntityId is null)
                    throw new ArgumentException("Parameter 'entity_id' is required for action 'unsubscribe'.");
                if (request.EventTypes is null || request.EventTypes.Count == 0)
                    throw new ArgumentException("Parameter 'event_types' is required for action 'unsubscribe'.");
                return await client.UnsubscribeFromEvents(request.EntityId!.ToString(), request.EventTypes);

            case EventOperationAction.RaiseEvent:
                if (request.EntityId is null)
                    throw new ArgumentException("Parameter 'entity_id' is required for action 'raise_event'.");
                if (request.EventType is null)
                    throw new ArgumentException("Parameter 'event_type' is required for action 'raise_event'.");
                return await client.RaiseEvent(request.EventType.Value, request.EntityId!.ToString());

            case EventOperationAction.RaiseCustomEvent:
                if (request.EntityId is null)
                    throw new ArgumentException("Parameter 'entity_id' is required for action 'raise_custom_event'.");
                if (request.CustomEventId is null)
                    throw new ArgumentException("Parameter 'custom_event_id' is required for action 'raise_custom_event'.");
                return await client.RaiseCustomEvent(request.CustomEventId.Value, request.EntityId!.ToString(), request.Message);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
