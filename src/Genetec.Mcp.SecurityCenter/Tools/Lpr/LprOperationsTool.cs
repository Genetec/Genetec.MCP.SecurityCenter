using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class LprOperationsTool
{
    [McpServerTool(
        Name = "lpr_operations",
        Title = "LPR Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Trigger LPR unit operations: trigger_read, trigger_past_read_matching.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR operations request")] LprOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case LprOperationsAction.TriggerRead:
                await client.RequireEntityType(request.UnitId, "unit_id", "Device");
                return await client.ExecuteAction($"TriggerReadOnLprUnit({request.UnitId})");

            case LprOperationsAction.TriggerPastReadMatching:
                await client.RequireEntityType(request.UnitId, "unit_id", "Role");
                return await client.ExecuteAction($"TriggerPastReadMatching({request.UnitId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum LprOperationsAction { TriggerRead, TriggerPastReadMatching }

public sealed record LprOperationsRequest(
    [property: Description("Action to perform"), Required] LprOperationsAction Action,
    [property: Description("LPR camera device GUID for trigger_read, or LPR Manager role GUID for trigger_past_read_matching"), Required] Guid UnitId);
