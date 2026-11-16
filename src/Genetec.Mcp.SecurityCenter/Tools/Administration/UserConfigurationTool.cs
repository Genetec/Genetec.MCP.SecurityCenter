using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserConfigurationTool
{
    [McpServerTool(
        Name = "user_configuration",
        Title = "User Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Configure user group membership, watermarking, PTZ priorities, domain, external ID, " +
        "camera security levels, and search. Actions: add_to_group, remove_from_group, " +
        "enable_watermarking, disable_watermarking, add_ptz_priority, remove_ptz_priority, " +
        "set_domain, set_external_id, get_security_level_for_camera, search. Use small result limits; do not fetch all entities automatically. Ask the user before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User configuration request")] UserConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case UserConfigurationAction.AddToGroup:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.UserGroupId is null) throw new ArgumentException("Parameter 'user_group_id' is required for this action.", "user_group_id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                await client.RequireEntityType(request.UserGroupId.Value, "user_group_id", "UserGroup");
                return await client.AddToCollection(request.UserGroupId!.ToString(), "Members", request.Id!.ToString());

            case UserConfigurationAction.RemoveFromGroup:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.UserGroupId is null) throw new ArgumentException("Parameter 'user_group_id' is required for this action.", "user_group_id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                await client.RequireEntityType(request.UserGroupId.Value, "user_group_id", "UserGroup");
                return await client.RemoveFromCollection(request.UserGroupId!.ToString(), "Members", request.Id!.ToString());

            case UserConfigurationAction.EnableWatermarking:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "EnableUserWatermarking");

            case UserConfigurationAction.DisableWatermarking:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "DisableUserWatermarking");

            case UserConfigurationAction.AddPtzPriority:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.PtzEntityId is null) throw new ArgumentException("Parameter 'ptz_entity_id' is required for this action.", "ptz_entity_id");
                if (request.PtzPriority is null) throw new ArgumentException("Parameter 'ptz_priority' is required for this action.", "ptz_priority");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "AddPtzPriority",
                    $"PtzPriority({request.PtzEntityId},{request.PtzPriority})");

            case UserConfigurationAction.RemovePtzPriority:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.PtzEntityId is null) throw new ArgumentException("Parameter 'ptz_entity_id' is required for this action.", "ptz_entity_id");
                if (request.PtzPriority is null) throw new ArgumentException("Parameter 'ptz_priority' is required for this action.", "ptz_priority");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "RemovePtzPriority",
                    $"PtzPriority({request.PtzEntityId},{request.PtzPriority})");

            case UserConfigurationAction.SetDomain:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.Domain is null) throw new ArgumentException("Parameter 'domain' is required for this action.", "domain");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "SetDomain",
                    SecurityCenterRestClient.EscapeQueryValue(request.Domain));

            case UserConfigurationAction.SetExternalId:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.ExternalId is null) throw new ArgumentException("Parameter 'external_id' is required for this action.", "external_id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "SetExternalUniqueId",
                    SecurityCenterRestClient.EscapeQueryValue(request.ExternalId));

            case UserConfigurationAction.GetSecurityLevelForCamera:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for this action.", "id");
                if (request.TargetEntityId is null) throw new ArgumentException("Parameter 'target_entity_id' is required for this action.", "target_entity_id");
                await client.RequireEntityType(request.Id.Value, "id", "User");
                await client.RequireEntityType(request.TargetEntityId.Value, "target_entity_id", "Camera");
                return await client.GetEntityField(request.Id!.ToString(), $"GetSecurityLevelForCamera({request.TargetEntityId})");

            case UserConfigurationAction.Search:
                if (request.NameSearchMode is not null && request.Name is null)
                    throw new ArgumentException("Parameter 'name' is required when 'name_search_mode' is specified.", "name");
                if (request.Status is not null && request.Status.Any(status => !Enum.IsDefined(status)))
                    throw new ArgumentException("Parameter 'status' must contain only activated or deactivated values.", "status");
                return await client.RunReport("UserReport", BuildSearchQuery(request.Name, request.Status, request.NameSearchMode, request.MaximumResultCount));

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

    private static string BuildSearchQuery(string? name, List<GeneralUserStatus>? status, EntityNameSearchMode? nameSearchMode, int maximumResultCount)
    {
        var parts = new List<string>();
        if (name != null)
        {
            parts.Add($"Name={SecurityCenterRestClient.EscapeQueryValue(name)}");
            parts.Add($"NameSearchMode={nameSearchMode?.ToString() ?? "Contains"}");
        }
        var statusQuery = QueryCollection("Status", status);
        if (statusQuery is not null) parts.Add(statusQuery);
        parts.Add($"MaximumResultCount={maximumResultCount}");
        return string.Join(",", parts);
    }
}

public enum UserConfigurationAction
{
    AddToGroup,
    RemoveFromGroup,
    EnableWatermarking,
    DisableWatermarking,
    AddPtzPriority,
    RemovePtzPriority,
    SetDomain,
    SetExternalId,
    GetSecurityLevelForCamera,
    Search
}

public sealed record UserConfigurationRequest(
    [property: Description("Action to perform"), Required] UserConfigurationAction Action,
    [property: Description("User GUID")] Guid? Id = null,
    [Description("Username (for search)")] string? Name = null,
    [property: Description("User group GUID")] Guid? UserGroupId = null,
    [property: Description("Camera or PTZ entity GUID")] Guid? PtzEntityId = null,
    [Description("PTZ priority level 1-255")] int? PtzPriority = null,
    [Description("Domain name")] string? Domain = null,
    [Description("External unique ID")] string? ExternalId = null,
    [property: Description("Camera GUID (for get_security_level_for_camera)")] Guid? TargetEntityId = null,
    [property: Description("User activation statuses to include in search: activated, deactivated"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<GeneralUserStatus>? Status = null,
    [Description("Name match mode for search")] EntityNameSearchMode? NameSearchMode = null,
    [property: Description("Maximum search results (default 50); use small limits and ask the user before large queries"), Range(1, 100000)] int MaximumResultCount = 50);
