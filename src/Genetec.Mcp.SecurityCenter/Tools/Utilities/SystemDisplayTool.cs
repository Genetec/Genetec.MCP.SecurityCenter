using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class SystemDisplayTool
{
    [McpServerTool(
        Name = "system_display",
        Title = "System Display Control",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Control what is displayed on Security Desk monitors and tiles. " +
                 "Actions: display_entity, clear_tile, get_tile, send_user_task_to_monitor.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("System display request")] SystemDisplayRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case DisplayAction.DisplayEntity:
                if (request.EntityId is null) throw new ArgumentException("Parameter 'entity_id' is required for action 'display_entity'.");
                if (request.DestinationId is null) throw new ArgumentException("Parameter 'destination_id' is required for action 'display_entity'.");
                await client.RequireEntityType(request.DestinationId.Value, "destination_id", "User", "UserGroup");
                return await client.ExecuteAction(
                    $"DisplayEntityInSecurityDesk({request.DestinationId},{request.EntityId},true)");

            case DisplayAction.ClearTile:
                if (request.MonitorId is null) throw new ArgumentException("Parameter 'monitor_id' is required for action 'clear_tile'.");
                if (request.TileId is null) throw new ArgumentException("Parameter 'tile_id' is required for action 'clear_tile'.");
                return await client.ExecuteAction($"ClearTile({request.MonitorId},{request.TileId})");

            case DisplayAction.GetTile:
                if (request.MonitorId is null) throw new ArgumentException("Parameter 'monitor_id' is required for action 'get_tile'.");
                if (request.TileId is null) throw new ArgumentException("Parameter 'tile_id' is required for action 'get_tile'.");
                return await client.ExecuteAction($"GetTile({request.MonitorId},{request.TileId})");

            case DisplayAction.SendUserTaskToMonitor:
                if (request.MonitorId is null) throw new ArgumentException("Parameter 'monitor_id' is required for action 'send_user_task_to_monitor'.");
                if (request.EntityId is null) throw new ArgumentException("Parameter 'entity_id' is required for action 'send_user_task_to_monitor'.");
                await client.RequireEntityType(request.EntityId.Value, "entity_id", "UserTask");
                return await client.ExecuteAction($"SendUserTaskToMonitor({request.MonitorId},{request.EntityId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum DisplayAction { DisplayEntity, ClearTile, GetTile, SendUserTaskToMonitor }

public sealed record SystemDisplayRequest(
    [property: Description("Action to perform"), Required] DisplayAction Action,
    [property: Description("Entity GUID to display or user task GUID (for send_user_task_to_monitor)")] Guid? EntityId = null,
    [property: Description("Recipient user or user group GUID (for display_entity: recipient who sees the display in Security Desk)")] Guid? DestinationId = null,
    [Description("Monitor logical ID integer (for clear_tile, get_tile, send_user_task_to_monitor)")] int? MonitorId = null,
    [Description("Tile index (for clear_tile, get_tile)")] int? TileId = null);
