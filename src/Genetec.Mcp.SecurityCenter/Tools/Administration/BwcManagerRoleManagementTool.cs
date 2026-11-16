using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class BwcManagerRoleManagementTool
{
    [McpServerTool(
        Name = "bwc_manager_role_management",
        Title = "BWC Manager Role Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage BWC Manager roles: list, get, and create. " +
                 "Provide one role name in items for a single create, or multiple names for a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("BWC Manager role management request")] BwcManagerRoleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case BwcManagerRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case BwcManagerRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Role");

            case BwcManagerRoleAction.Create:
                return await CreateToolHelper.CreateRole(client, "BwcManager", request.Items);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

}

public enum BwcManagerRoleAction
{
    List,
    Get,
    Create
}

public sealed record BwcManagerRoleManagementRequest(
    [property: Description("Action to perform"), Required] BwcManagerRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("One or more role names for create"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
