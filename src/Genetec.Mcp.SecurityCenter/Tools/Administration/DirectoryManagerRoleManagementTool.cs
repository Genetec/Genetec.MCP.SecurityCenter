using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class DirectoryManagerRoleManagementTool
{
    [McpServerTool(
        Name = "directory_manager_role_management",
        Title = "Directory Manager Role Management",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Manage Directory Manager roles: list and get.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Directory Manager role management request")] DirectoryManagerRoleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case DirectoryManagerRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case DirectoryManagerRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Role");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }
}

public enum DirectoryManagerRoleAction
{
    List,
    Get,
}

public sealed record DirectoryManagerRoleManagementRequest(
    [property: Description("Action to perform"), Required] DirectoryManagerRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
