using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class DoorTemplateManagementTool
{
    [McpServerTool(
        Name = "door_template_management",
        Title = "Door Template Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage door templates: list, get, create, update, delete, apply, get_access_points, get_access_rules, get_card_pin_settings, get_applied_doors")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door template management request")] DoorTemplateManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case DoorTemplateAction.List:
                return await client.ListEntities("DoorTemplate", request.PageSize, request.Page);

            case DoorTemplateAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntity(request.Id.Value, "DoorTemplate");

            case DoorTemplateAction.Create:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                var names = ManagementBatchItemHelper.GetCreateNames(items);
                if (items.Any(item => item.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var creates = items.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "DoorTemplate", creates);
            }

            case DoorTemplateAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<DoorTemplateUpdateProperties>(client, items, "DoorTemplate",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) });
            }

            case DoorTemplateAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<DoorTemplateUpdateProperties>(items), "DoorTemplate");
            }

            case DoorTemplateAction.Apply:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for apply.");
                if (request.DoorId is null) throw new ArgumentException("Parameter 'door_id' is required for apply.");
                if (request.UnitId is null) throw new ArgumentException("Parameter 'unit_id' is required for apply.");
                if (request.InterfaceModuleId is null)
                    throw new ArgumentException("Parameter 'interface_module_id' is required for apply.");

                await client.RequireEntityType(request.Id.Value, "id", "DoorTemplate");
                await client.RequireEntityType(request.DoorId.Value, "door_id", "Door");
                await client.RequireEntityType(request.UnitId.Value, "unit_id", "Unit");
                await client.RequireEntityType(request.InterfaceModuleId.Value, "interface_module_id", "InterfaceModule");
                return await client.ExecuteEntityMethod(
                    request.Id.Value.ToString(), "Apply", BuildApplyArguments(request));

            case DoorTemplateAction.GetAccessPoints:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_access_points.");
                await client.RequireEntityType(request.Id.Value, "id", "DoorTemplate");
                return await client.GetEntityField(request.Id!.ToString(), "GetCurrentAccessPointSelectors()");

            case DoorTemplateAction.GetAccessRules:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_access_rules.");
                await client.RequireEntityType(request.Id.Value, "id", "DoorTemplate");
                return await client.GetEntityField(request.Id!.ToString(), "GetCurrentAccessRuleDescriptors()");

            case DoorTemplateAction.GetCardPinSettings:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_card_pin_settings.");
                await client.RequireEntityType(request.Id.Value, "id", "DoorTemplate");
                return await client.GetEntityField(request.Id!.ToString(), "GetCurrentCardAndPinSettings()");

            case DoorTemplateAction.GetAppliedDoors:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_applied_doors.");
                await client.RequireEntityType(request.Id.Value, "id", "DoorTemplate");
                return await client.GetEntityField(request.Id!.ToString(), "GetAppliedDoors()");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    // Apply's arguments are method parameters, so commas must remain unescaped.
    // Escaping them makes the Web SDK treat the whole argument list as one value.
    internal static string BuildApplyArguments(DoorTemplateManagementRequest request)
    {
        var applyArgs = string.Join(",", new[]
        {
            request.DoorId!.Value.ToString(),
            request.UnitId!.Value.ToString(),
            request.InterfaceModuleId!.Value.ToString()
        });

        return request.ApplyAccessRules.HasValue
            ? $"{applyArgs},{request.ApplyAccessRules.Value.ToString().ToLowerInvariant()}"
            : applyArgs;
    }
}

public enum DoorTemplateAction
{
    Get,
    List,
    Create,
    Update,
    Delete,
    Apply,
    GetAccessPoints,
    GetAccessRules,
    GetCardPinSettings,
    GetAppliedDoors
}



public sealed record DoorTemplateManagementItem(
    [property: Description("DoorTemplate name for create"), MinLength(1)] string? Name = null,
    [property: Description("DoorTemplate GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed DoorTemplate properties for update")] DoorTemplateUpdateProperties? Body = null) : IManagementCrudItem<DoorTemplateUpdateProperties>;

public sealed record DoorTemplateManagementRequest(
    [property: Description("Action to perform"), Required] DoorTemplateAction Action,
    [property: Description("Door template GUID for reads or apply")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<DoorTemplateManagementItem>? Items = null,
    [property: Description("Door GUID to apply the template to")] Guid? DoorId = null,
    [property: Description("Access control unit GUID for the door")] Guid? UnitId = null,
    [property: Description("Interface module GUID for the door")] Guid? InterfaceModuleId = null,
    [property: Description("Whether to apply the template's access rules")] bool? ApplyAccessRules = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record DoorTemplateUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Door forced behavior")] string? DoorForcedBehavior = null,
    [Description("Door held behavior")] string? DoorHeldBehavior = null,
    [Description("Door held-open delay in seconds")] uint? DoorHeldOpenDelay = null,
    [Description("Door relock delay in seconds")] uint? DoorRelockDelay = null,
    [Description("Whether to relock the door when it closes")] bool? DoorRelockOnClose = null,
    [Description("Enforce visitor escort rules on the alpha side")] bool? EnforceVisitorEscortRulesAlphaSide = null,
    [Description("Enforce visitor escort rules on the beta side")] bool? EnforceVisitorEscortRulesBetaSide = null,
    [Description("Extended entry time in seconds")] uint? ExtendedEntryTime = null,
    [Description("Extended grant time in seconds")] uint? ExtendedGrantTime = null,
    [Description("Grant access on REX")] bool? GrantOnRex = null,
    [Description("Interface module device definition GUID")] Guid? InterfaceModuleDeviceDefinition = null,
    [Description("Whether access rules are enabled")] bool? IsAccessRulesEnabled = null,
    [Description("Whether this is the default template")] bool? IsDefault = null,
    [Description("Whether door properties are enabled")] bool? IsDoorPropertiesEnabled = null,
    [Description("Whether hardware mapping is enabled")] bool? IsHardwareMappingEnabled = null,
    [Description("Whether hardware settings are enabled")] bool? IsHardwareSettingsEnabled = null,
    [Description("Mask access while unlocked by schedule")] bool? MaskAccessWhenUnlockedBySchedule = null,
    [Description("Mask door-open-too-long while unlocked by schedule")] bool? MaskDoorOpenTooLongWhenUnlockedBySchedule = null,
    [Description("Maximum delay between swipes in seconds")] uint? MaximumDelayBetweenSwipes = null,
    [Description("Normal entry time in seconds")] uint? NormalEntryTime = null,
    [Description("Normal grant time in seconds")] uint? NormalGrantTime = null,
    [Description("REX hold-off after closed delay in seconds")] uint? RexHoldOffAfterClosedDelay = null,
    [Description("REX hold-off after grant delay in seconds")] uint? RexHoldOffAfterGrantDelay = null,
    [Description("Hold off REX while the door is open")] bool? RexHoldoffWhileOpened = null);
