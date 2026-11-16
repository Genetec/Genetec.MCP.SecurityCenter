using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class IntrusionUnitManagementTool
{
    [McpServerTool(
        Name = "intrusion_unit_management",
        Title = "Intrusion Unit Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage intrusion units: list, get, update, delete, get_area, move")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Intrusion unit management request")] IntrusionUnitManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case IntrusionUnitAction.List:
                return await client.ListEntities("IntrusionUnit", request.PageSize, request.Page);

            case IntrusionUnitAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "IntrusionUnit");

            case IntrusionUnitAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<IntrusionUnitUpdateProperties>(client, items, "IntrusionUnit",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) });
            }

            case IntrusionUnitAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<IntrusionUnitUpdateProperties>(items), "IntrusionUnit");
            }

            case IntrusionUnitAction.GetArea:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_area'.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "IntrusionUnit");
                return await client.GetEntityField(request.Id!.ToString(), "IntrusionAreas");

            case IntrusionUnitAction.Move:
                if (!request.Id.HasValue)
                    throw new ArgumentException("Parameter 'id' is required for action move.", "id");
                if (!request.TargetUnitId.HasValue)
                    throw new ArgumentException("Parameter 'target_unit_id' is required for action move.", "target_unit_id");
                await client.RequireEntityType(request.Id.Value, "id", "IntrusionUnit");
                await client.RequireEntityType(request.TargetUnitId.Value, "target_unit_id", "Role");
                return await client.ExecuteAction($"MoveIntrusionUnit({request.Id},{request.TargetUnitId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum IntrusionUnitAction
{
    Get,
    List,
    Update,
    Delete,
    GetArea,
    Move
}

public sealed record IntrusionUnitManagementItem(
    [property: Description("IntrusionUnit GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed IntrusionUnit properties for update")] IntrusionUnitUpdateProperties? Body = null) : IManagementCrudItem<IntrusionUnitUpdateProperties>
{
    string? IManagementBatchItem.Name => null;
}

public sealed record IntrusionUnitManagementRequest(
    [property: Description("Action to perform"), Required] IntrusionUnitAction Action,
    [property: Description("Intrusion unit GUID for reads or move")] Guid? Id = null,
    [property: Description("Destination Intrusion Manager role GUID for move")] Guid? TargetUnitId = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<IntrusionUnitManagementItem>? Items = null);

public sealed record IntrusionUnitUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Intrusion unit capabilities")] string? Capabilities = null,
    [Description("AC failure state")] bool? ACFailure = null,
    [Description("Battery failure state")] bool? BatteryFailure = null,
    [Description("Clear intrusion unit logs after download")] bool? ClearLogsAfterDownload = null,
    [Description("Command port")] int? CommandPort = null,
    [Description("Intrusion unit identifier")] string? IntrusionUnitId = null,
    [Description("Whether the intrusion unit is activated")] bool? IsActivated = null,
    [Description("Serial port")] string? SerialPort = null,
    [Description("Tamper state")] bool? Tamper = null);
