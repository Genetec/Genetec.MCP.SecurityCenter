using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class ParkingZoneTool
{
    [McpServerTool(Name = "parking_zone", Title = "Parking Zone",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Manage parking zone entities: list, get, create, update, delete. Use items for all create, update, and delete operations; one item selects a single operation and multiple items select a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Parking zone management request")] ParkingZoneManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case ParkingZoneAction.List:
                return await client.ListEntities("ParkingZone", request.PageSize, request.Page);
            case ParkingZoneAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for get.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "ParkingZone");
            case ParkingZoneAction.Create:
            {
                RequireItems(request);
                var names = ManagementBatchItemHelper.GetCreateNames(request.Items!);
                if (request.Items!.Any(item => item?.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var items = request.Items!.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "ParkingZone", items);
            }
            case ParkingZoneAction.Update:
            {
                RequireItems(request);
                foreach (var item in request.Items!)
                {
                    if (item is null || item.Id is null)
                        throw new ArgumentException("Each item must include an id for update.", "items");
                    if (item.Body is null && string.IsNullOrWhiteSpace(item.Name))
                        throw new ArgumentException("Each item must include a body or name for update.", "items");
                    if (item.Body is not null && item.Name is not null)
                        throw new ArgumentException("Provide either 'items.body' or 'items.name', not both.", "items");
                }
                object GetBody(ParkingZoneBatchItem item) => item.Body is { } properties
                    ? properties with { Name = properties.Name is null ? null : EntityNameSanitizer.Sanitize(properties.Name) }
                    : new { Name = EntityNameSanitizer.Sanitize(item.Name!) };
                if (request.Items.Count == 1)
                {
                    var item = request.Items[0];
                    await client.RequireEntityType(item.Id!.Value, "items", "ParkingZone");
                    await RequireBodyTypes(client, item.Body);
                    return await client.UpdateEntity(item.Id.Value.ToString(), GetBody(item));
                }
                var updates = request.Items.Select(item => (item.Id!.Value, GetBody(item))).ToList();
                return await client.UpdateEntitiesBatch(updates, "ParkingZone",
                    index => RequireBodyTypes(client, request.Items[index].Body));
            }
            case ParkingZoneAction.Delete:
                RequireItems(request);
                if (request.Items!.Any(item => item?.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                return await DeleteToolHelper.Execute(client, ManagementBatchItemHelper.GetDeleteIds(request.Items), "ParkingZone");
            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

    private static void RequireItems(ParkingZoneManagementRequest request)
    {
        if (request.Id is not null)
            throw new ArgumentException("Parameter 'id' is only valid for get; use 'items' for create, update, and delete.", "id");
        if (request.Items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create, update, and delete.", "items");
    }

    private static async Task RequireBodyTypes(SecurityCenterRestClient client, ParkingZoneUpdateProperties? body)
    {
        if (body is null) return;
        if (body.Hotlist is { } hotlistId)
            await client.RequireEntityType(hotlistId, "items.body.hotlist", "HotlistRule");
        if (body.LprManager is { } lprManagerId)
            await client.RequireEntityType(lprManagerId, "items.body.lpr_manager", "Role");
    }
}

public sealed record ParkingZoneBatchItem(
    [property: Description("ParkingZone name for create or rename"), MinLength(1)] string? Name = null,
    [property: Description("ParkingZone GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed ParkingZone properties for update")] ParkingZoneUpdateProperties? Body = null) : IManagementBatchItem;

public enum ParkingZoneAction { Get, List, Create, Update, Delete }

public sealed record ParkingZoneManagementRequest(
    [property: Description("Action to perform"), Required] ParkingZoneAction Action,
    [property: Description("ParkingZone GUID for get")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ParkingZoneBatchItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record ParkingZoneUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Parking zone capacity")] uint? Capacity = null,
    [Description("Hotlist rule GUID")] Guid? Hotlist = null,
    [Description("LPR Manager role GUID")] Guid? LprManager = null,
    [Description("Maximum inventory duration")] TimeSpan? MaxInventoryDuration = null);
