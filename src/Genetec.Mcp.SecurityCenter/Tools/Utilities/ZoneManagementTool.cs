using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class ZoneManagementTool
{
    [McpServerTool(Name = "zone_management", Title = "Zone Management",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Manage zone entities: list, get, create, update, delete. Use items for all create, update, and delete operations; one item selects a single operation and multiple items select a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Zone management request")] ZoneManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case ZoneAction.List:
                return await client.ListEntities("Zone", request.PageSize, request.Page);
            case ZoneAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for get.", "id");
                return await client.GetEntity(request.Id.Value, "Zone");
            case ZoneAction.Create:
            {
                RequireItems(request);
                var names = ManagementBatchItemHelper.GetCreateNames(request.Items!);

                var items = request.Items!.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]), item.Body)).ToList();
                return await CreateToolHelper.Execute(client, "Zone", items, request.Items.Any(item => item?.Body is not null)
                    ? index => RequireBodyTypes(client, request.Items[index].Body)
                    : null);
            }
            case ZoneAction.Update:
            {
                RequireItems(request);
                foreach (var item in request.Items!)
                {
                    if (item is null || item.Id is null)
                        throw new ArgumentException("Each item must include an id for update.", "items");
                    if (item.Body is null || item.Name is not null)
                        throw new ArgumentException("Each item must include a body and no name for update.", "items");
                }
                object GetBody(ZoneBatchItem item) => item.Body! with
                    { Name = item.Body.Name is null ? null : EntityNameSanitizer.Sanitize(item.Body.Name) };
                if (request.Items.Count == 1)
                {
                    var item = request.Items[0];
                    await client.RequireEntityType(item.Id!.Value, "items", "Zone");
                    await RequireBodyTypes(client, item.Body);
                    return await client.UpdateEntity(item.Id.Value.ToString(), GetBody(item));
                }
                var updates = request.Items.Select(item => (item.Id!.Value, GetBody(item))).ToList();
                return await client.UpdateEntitiesBatch(updates, "Zone",
                    index => RequireBodyTypes(client, request.Items[index].Body));
            }
            case ZoneAction.Delete:
                RequireItems(request);
                if (request.Items!.Any(item => item?.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for create or update.", "items.body");
                return await DeleteToolHelper.Execute(client, ManagementBatchItemHelper.GetDeleteIds(request.Items), "Zone");
            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

    private static void RequireItems(ZoneManagementRequest request)
    {
        if (request.Id is not null)
            throw new ArgumentException("Parameter 'id' is only valid for get; use 'items' for create, update, and delete.", "id");
        if (request.Items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create, update, and delete.", "items");
    }

    private static async Task RequireBodyTypes(SecurityCenterRestClient client, ZoneUpdateProperties? body)
    {
        if (body is null) return;
        if (body.AccessControlUnit is { } accessControlUnitId)
            await client.RequireEntityType(accessControlUnitId, "items.body.access_control_unit", "Unit");
        if (body.InterfaceModule is { } interfaceModuleId)
            await client.RequireEntityType(interfaceModuleId, "items.body.interface_module", "InterfaceModule");
        if (body.Owner is { } ownerId)
            await client.RequireEntityType(ownerId, "items.body.owner", "Role");
    }
}

public sealed record ZoneBatchItem(
    [property: Description("Zone name for create"), MinLength(1)] string? Name = null,
    [property: Description("Zone GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed Zone properties for create or update")] ZoneUpdateProperties? Body = null) : IManagementBatchItem;

public enum ZoneAction { List, Get, Create, Delete, Update }

public sealed record ZoneManagementRequest(
    [property: Description("Action to perform"), Required] ZoneAction Action,
    [property: Description("Zone GUID for get")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ZoneBatchItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public enum ZoneType { Hardware, Software, IO }

public sealed record ZoneUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Access control unit GUID")] Guid? AccessControlUnit = null,
    [Description("Activate the configured action on disarmed trouble")] bool? ActiveActionOnDisarmedTrouble = null,
    [Description("Active event ID")] int? ActiveState = null,
    [Description("Arming delay")] TimeSpan? ArmingDelay = null,
    [Description("Whether arming delay is enabled")] bool? ArmingDelayOn = null,
    [Description("Arming input GUID")] Guid? ArmingInput = null,
    [Description("Countdown sounder GUID")] Guid? CountdownSounder = null,
    [Description("Entry delay")] TimeSpan? EntryDelay = null,
    [Description("Whether entry delay is enabled")] bool? EntryDelayOn = null,
    [Description("Interface module GUID")] Guid? InterfaceModule = null,
    [Description("Normal event ID")] int? NormalState = null,
    [Description("Whether the input is on")] bool? OnInput = null,
    [Description("Active output behavior GUID")] Guid? OutputBehaviour = null,
    [Description("Inactive output behavior GUID")] Guid? OutputBehaviourInactive = null,
    [Description("Zone owner role GUID")] Guid? Owner = null,
    [Description("Zone threshold")] int? Threshold = null,
    [Description("Trouble event ID")] int? TroubleState = null,
    [Description("Zone formula")] string? ZoneFormula = null,
    [Description("Online behavior")] bool? ZoneOnlineBehavior = null,
    [Description("Zone type")] ZoneType? ZoneType = null,
    [Description("Whether I/O linking is synchronized")] bool? IOLinkingSynch = null);
