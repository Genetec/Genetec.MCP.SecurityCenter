using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class LprManagerRoleManagementTool
{
    [McpServerTool(
        Name = "lpr_manager_role_management",
        Title = "LPR Manager Role Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage LPR Manager roles: list, get, and create. " +
                 "Provide one role name in items for a single create, or multiple names for a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR Manager role management request")] LprManagerRoleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case LprManagerRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case LprManagerRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Role");

            case LprManagerRoleAction.Create:
                return await CreateToolHelper.CreateRole(
                    client, "LicensePlateManagement", request.Items, request.ServerId);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }
}

public enum LprManagerRoleAction
{
    List,
    Get,
    Create,
}

public sealed record LprManagerRoleManagementRequest(
    [property: Description("Action to perform"), Required] LprManagerRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("Server GUID assigned to every role created by this request")] Guid? ServerId = null,
    [property: Description("One or more role names for create"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
