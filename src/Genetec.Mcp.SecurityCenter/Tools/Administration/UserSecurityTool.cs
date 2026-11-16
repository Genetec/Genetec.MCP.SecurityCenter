using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

public enum UserSecurityAction
{
    SetPassword,
    GetAccessRights,
    Activate,
    Deactivate,
    GetStatus,
    IsAdministrator,
    HasAccess,
    HasPrivilege,
    SetPrivilege
}

public enum UserPrivilegeState { Denied, Granted, Undefined }

public sealed record UserSecurityRequest(
    [property: Description("Action to perform"), Required] UserSecurityAction Action,
    [property: Description("User GUID"), Required] Guid Id,
    [Description("New password (for set_password)")] string? Password = null,
    [property: Description("Privilege GUID (required for has_privilege, set_privilege)")] Guid? PrivilegeId = null,
    [property: Description("Target entity GUID (for has_access)")] Guid? TargetEntityId = null,
    [property: Description("Privilege state: granted, denied, or undefined (required for set_privilege). Undefined removes the explicit grant or denial so group inheritance applies.")] UserPrivilegeState? PrivilegeState = null);

[McpServerToolType]
public class UserSecurityTool
{
    [McpServerTool(
        Name = "user_security",
        Title = "User Security",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage user security settings including passwords, status, and access rights. " +
        "Actions: set_password, get_access_rights, activate, deactivate, get_status, " +
        "is_administrator, has_access, has_privilege, set_privilege. " +
        "set_privilege changes only the specified privilege; requires administration rights over the user. " +
        "has_privilege checks privilege availability and licensing, not the user's effective grant.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User security request parameters")] UserSecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case UserSecurityAction.SetPassword:
                if (string.IsNullOrEmpty(request.Password))
                    throw new ArgumentException("Parameter 'password' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.UpdateEntity(request.Id.ToString(), new { Password = request.Password });

            case UserSecurityAction.GetAccessRights:
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.GetEntityField(request.Id.ToString(), "Privileges");

            case UserSecurityAction.Activate:
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.SetEntityField(request.Id.ToString(), "UserStatus", "Activated");

            case UserSecurityAction.Deactivate:
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.SetEntityField(request.Id.ToString(), "UserStatus", "Deactivated");

            case UserSecurityAction.GetStatus:
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.GetEntityField(request.Id.ToString(), "UserStatus");

            case UserSecurityAction.IsAdministrator:
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.GetEntityField(request.Id.ToString(), "IsAdministrator");

            case UserSecurityAction.HasAccess:
                if (request.TargetEntityId is null)
                    throw new ArgumentException("Parameter 'target_entity_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.GetEntityField(request.Id.ToString(), $"HasAccess({request.TargetEntityId})");

            case UserSecurityAction.HasPrivilege:
                if (request.PrivilegeId is null)
                    throw new ArgumentException("Parameter 'privilege_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.GetEntityField(request.Id.ToString(), $"HasPrivilege({request.PrivilegeId})");

            case UserSecurityAction.SetPrivilege:
                if (request.PrivilegeId is null)
                    throw new ArgumentException("Parameter 'privilege_id' is required for action 'set_privilege'.");
                if (request.PrivilegeState is null)
                    throw new ArgumentException("Parameter 'privilege_state' is required for action 'set_privilege'.");
                if (!Enum.IsDefined(request.PrivilegeState.Value))
                    throw new ArgumentException("Parameter 'privilege_state' must be granted, denied, or undefined.");
                await client.RequireEntityType(request.Id, "id", "User");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetPrivilegeState",
                    $"{request.PrivilegeId.Value},{request.PrivilegeState.Value},Default");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
