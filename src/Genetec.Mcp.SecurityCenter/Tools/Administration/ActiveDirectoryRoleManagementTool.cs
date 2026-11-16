using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class ActiveDirectoryRoleManagementTool
{
    [McpServerTool(
        Name = "active_directory_role_management",
        Title = "Active Directory Role Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage Active Directory roles: list, get, create, sync. " +
                 "Provide one role name in items for a single create, or multiple names for a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Active Directory role management request")] ActiveDirectoryRoleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ActiveDirectoryRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case ActiveDirectoryRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Role");

            case ActiveDirectoryRoleAction.Create:
                return await CreateToolHelper.CreateRole(
                    client, "ActiveDirectory", request.Items, request.ServerId);

            case ActiveDirectoryRoleAction.Sync:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'sync'.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "Role");
                return await client.SynchronizeActiveDirectory(request.Id.Value, request.SynchronizeImages);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }
}

public enum ActiveDirectoryRoleAction
{
    List,
    Get,
    Create,
    Sync
}

public sealed record ActiveDirectoryRoleManagementRequest(
    [property: Description("Action to perform"), Required] ActiveDirectoryRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("Server GUID assigned to every role created by this request")] Guid? ServerId = null,
    [property: Description("One or more role names for create"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("Synchronize cardholder profile images from Active Directory for sync (default false)")] bool SynchronizeImages = false);
