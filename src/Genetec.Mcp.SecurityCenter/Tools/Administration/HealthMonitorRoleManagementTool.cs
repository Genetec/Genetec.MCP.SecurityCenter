using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class HealthMonitorRoleManagementTool
{
    [McpServerTool(
        Name = "health_monitor_role_management",
        Title = "Health Monitor Role Management",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Manage Health Monitor roles: list, get, and get_event_report.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Health Monitor role management request")] HealthMonitorRoleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case HealthMonitorRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case HealthMonitorRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Role");

            case HealthMonitorRoleAction.GetEventReport:
                return await client.RunReport("HealthEvents", "MaximumResultCount=50");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

}

public enum HealthMonitorRoleAction
{
    List,
    Get,
    GetEventReport
}

public sealed record HealthMonitorRoleManagementRequest(
    [property: Description("Action to perform"), Required] HealthMonitorRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
