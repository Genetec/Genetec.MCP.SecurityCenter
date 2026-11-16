using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class ParkingOperationsTool
{
    [McpServerTool(
        Name = "parking_operations",
        Title = "Parking Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Execute parking zone operations: reset_inventory, set_occupancy (requires occupancy), " +
                 "trigger_enforce_violation (requires session_ids: list of parking session GUIDs in violation).")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Parking operations request")] ParkingOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case ParkingOperationsAction.ResetInventory:
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                return await client.ExecuteAction($"ResetParkingZoneInventory({request.ZoneId})");

            case ParkingOperationsAction.SetOccupancy:
                if (request.Occupancy is null) throw new ArgumentException("Parameter 'occupancy' is required for this action.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                return await client.ExecuteAction($"SetParkingZoneOccupancy({request.ZoneId},{request.Occupancy})");

            case ParkingOperationsAction.TriggerEnforceViolation:
                if (request.SessionIds is null || request.SessionIds.Count == 0)
                    throw new ArgumentException("Parameter 'session_ids' is required and must contain at least one parking session GUID.");
                ResourceLimits.EnsureBatchOperationCount(request.SessionIds.Count, "session_ids");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                var sessionList = string.Join("@", request.SessionIds);
                return await client.ExecuteAction($"TriggerEnforceViolationAction({request.ZoneId},{sessionList})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum ParkingOperationsAction { ResetInventory, SetOccupancy, TriggerEnforceViolation }

public sealed record ParkingOperationsRequest(
    [property: Description("Parking zone GUID"), Required] Guid ZoneId,
    [property: Description("Action to perform"), Required] ParkingOperationsAction Action,
    [Description("Occupancy count (required for set_occupancy)")] int? Occupancy = null,
    [property: Description("Parking session GUIDs in violation (required for trigger_enforce_violation)"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? SessionIds = null);
