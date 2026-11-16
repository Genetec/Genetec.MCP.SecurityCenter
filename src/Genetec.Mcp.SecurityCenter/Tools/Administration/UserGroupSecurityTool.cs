using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

public enum UserGroupSecurityAction
{
    GetAccessRights,
    HasAccess,
    HasPrivilege,
    EnableWatermarking,
    DisableWatermarking,
    AddPtzPriority,
    RemovePtzPriority
}

public sealed record UserGroupSecurityRequest(
    [property: Description("Action to perform"), Required] UserGroupSecurityAction Action,
    [property: Description("User group GUID"), Required] Guid Id,
    [property: Description("Privilege GUID (for has_privilege)")] Guid? PrivilegeId = null,
    [property: Description("Target entity GUID (for has_access)")] Guid? TargetEntityId = null,
    [property: Description("Camera or PTZ entity GUID (for add_ptz_priority, remove_ptz_priority)")] Guid? PtzEntityId = null,
    [Description("PTZ priority level 1-255 (for add_ptz_priority, remove_ptz_priority)")] int? PtzPriority = null);

[McpServerToolType]
public class UserGroupSecurityTool
{
    [McpServerTool(
        Name = "user_group_security",
        Title = "User Group Security",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage user group security settings including access rights, watermarking, and PTZ priorities. " +
        "Actions: get_access_rights, has_access, has_privilege, enable_watermarking, disable_watermarking, add_ptz_priority, remove_ptz_priority")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group security request")] UserGroupSecurityRequest request,
        string? sessionAlias = null,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case UserGroupSecurityAction.GetAccessRights:
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.GetEntityField(request.Id.ToString(), "Privileges");

            case UserGroupSecurityAction.HasAccess:
                if (request.TargetEntityId is null)
                    throw new ArgumentException("target_entity_id is required for has_access.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.GetEntityField(request.Id.ToString(), $"HasAccess({request.TargetEntityId})");

            case UserGroupSecurityAction.HasPrivilege:
                if (request.PrivilegeId is null)
                    throw new ArgumentException("privilege_id is required for has_privilege.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.GetEntityField(request.Id.ToString(), $"HasPrivilege({request.PrivilegeId})");

            case UserGroupSecurityAction.EnableWatermarking:
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "EnableUserWatermarking");

            case UserGroupSecurityAction.DisableWatermarking:
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "DisableUserWatermarking");

            case UserGroupSecurityAction.AddPtzPriority:
                if (request.PtzEntityId is null)
                    throw new ArgumentException("ptz_entity_id is required for add_ptz_priority.");
                if (request.PtzPriority is null)
                    throw new ArgumentException("ptz_priority is required for add_ptz_priority.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddPtzPriority", $"PtzPriority({request.PtzEntityId},{request.PtzPriority})");

            case UserGroupSecurityAction.RemovePtzPriority:
                if (request.PtzEntityId is null)
                    throw new ArgumentException("ptz_entity_id is required for remove_ptz_priority.");
                if (request.PtzPriority is null)
                    throw new ArgumentException("ptz_priority is required for remove_ptz_priority.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemovePtzPriority", $"PtzPriority({request.PtzEntityId},{request.PtzPriority})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
