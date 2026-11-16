using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class EntitySearchTool
{
    [McpServerTool(
        Name = "entity_search",
        Title = "Entity Search",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Search and inspect entities: search_by_type, search_by_name, search_by_logical_id, get_entity, get_fields, get_fields_batch. Use small pages; do not fetch all entities automatically. Ask the user before large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Entity search request")] EntitySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case EntitySearchAction.SearchByType:
                if (request.EntityType is null || request.EntityType.Count == 0)
                    throw new ArgumentException("Parameter 'entity_type' is required for action 'search_by_type'.");
                return await client.ListEntities(request.EntityType.Select(ToWebSdkEntityType).ToList(), request.PageSize, request.Page);

            case EntitySearchAction.SearchByName:
                return await client.SearchEntities(request.Name,
                    request.EntityType?.Count > 0 ? request.EntityType.Select(ToWebSdkEntityType).ToList() : null,
                    request.NameSearchMode?.ToString(), request.PageSize, request.Page);

            case EntitySearchAction.SearchByLogicalId:
                if (request.LogicalId is null)
                    throw new ArgumentException("Parameter 'logical_id' is required for action 'search_by_logical_id'.");
                if (request.EntityType is null || request.EntityType.Count == 0)
                    throw new ArgumentException("Parameter 'entity_type' is required for action 'search_by_logical_id'.");
                return await client.GetEntityByLogicalId(ToWebSdkEntityType(request.EntityType[0]), request.LogicalId);

            case EntitySearchAction.GetEntity:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_entity.");
                return await client.GetEntity(request.Id.ToString());

            case EntitySearchAction.GetFields:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get_fields.");
                if (request.Fields is null or { Count: 0 }) throw new ArgumentException("Parameter 'fields' is required for action 'get_fields'.");
                return await client.GetEntityFields(request.Id.ToString(), [.. request.Fields]);

            case EntitySearchAction.GetFieldsBatch:
                if (request.Items is null || request.Items.Count == 0) throw new ArgumentException("Parameter 'items' is required for action 'get_fields_batch'.");
                if (request.Items.Count > 50) throw new ArgumentException("Batch get_fields supports up to 50 items per request.");
                return await client.GetEntityFieldsBatch(
                    request.Items.Select(i => (i.Id.ToString(), i.Fields)).ToList());

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static string ToWebSdkEntityType(ScEntityType entityType)
        => entityType switch
        {
            ScEntityType.AccessPointRule => "AccessRule",
            _ => entityType.ToString()
        };
}

public enum ScEntityType
{
    UserGroup, User, Application, Schedule, AccessControlUnit, Device,
    Cardholder, CardholderGroup, Credential, AccessPoint, AccessPointRule,
    SystemConfig, Partition, Alarm, Camera, Role, OutputBehavior, Door,
    Area, Elevator, Zone, Visitor, ScheduledTask, Macro, TilePlugin, Badge,
    UserTask, File, CashRegister, Patroller, Permit, LprUnit, LprHotlistRule,
    LprOvertimeRule, LprPermitRule, LprSharedPermitRule, LprMlpiRule, Network,
    Route, Keyboard, Monitor, Sequence, Stream, IntrusionUnit, IntrusionArea,
    Server, VideoUnit, Agent, Asset, ArchivedStream, MonitorGroup, AnalogMonitor,
    ThreatLevel, InterfaceModule, TransferGroup, CustomEntity, Map, FusionStream,
    ParkingZone, ParkingZoneRule, TileLayout, DoorTemplate,
    InterfaceModuleDeviceDefinition, Contact, Workstation, Endpoint, ActionPlan,
    Folder, EventFilterConfiguration, MapLayer, CaptainTablet
}

public enum EntityNameSearchMode
{
    Contains,
    StartsWith,
    EndsWith,
    Is
}

public enum EntitySearchAction
{
    SearchByType,
    SearchByName,
    SearchByLogicalId,
    GetEntity,
    GetFields,
    GetFieldsBatch
}

public sealed record EntitySearchRequest(
    [property: Description("Action to perform"), Required] EntitySearchAction Action,
    [property: Description("Entity GUID")] Guid? Id = null,
    [property: Description("Entity types to filter by"), MaxLength(ResourceLimits.MaxBatchItems)] List<ScEntityType>? EntityType = null,
    [Description("Name to search")] string? Name = null,
    [Description("Logical ID")] string? LogicalId = null,
    [Description("Name search mode")] EntityNameSearchMode? NameSearchMode = null,
    [property: Description("Field names to retrieve (for get_fields)"), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Fields = null,
    [property: Description("Batch items (for get_fields_batch)"), MaxLength(ResourceLimits.MaxBatchItems)] List<EntityFieldsBatchItem>? Items = null,
    [property: Description("Max results per page (1–100, default 50). Use small pages; ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record EntityFieldsBatchItem(
    [property: Description("Entity GUID"), Required] Guid Id,
    [property: Description("Field names to retrieve"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string> Fields);
