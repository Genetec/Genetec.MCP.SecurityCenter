using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Events;

public sealed record EventStreamRequest(
    [property: Description("How long to collect events in seconds"), Range(1, 30)] int TimeoutSeconds = 10);

[McpServerToolType]
public class EventStreamTool
{
    private const int MaxEvents = 50;

    [McpServerTool(
        Name = "event_stream",
        Title = "Stream Events",
        ReadOnly = true,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Open the Security Center event stream and collect events for the specified duration. " +
        "Call event_subscribe first to register which entity events you want to receive. " +
        "Returns up to 50 events that fired during the collection window.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Event stream request")] EventStreamRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.StreamEventsAsync(request.TimeoutSeconds, MaxEvents);
    }
}
