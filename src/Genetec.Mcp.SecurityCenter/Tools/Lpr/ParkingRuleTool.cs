using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class ParkingRuleTool
{
    [McpServerTool(Name = "parking_rule", Title = "Parking Rule",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Manage parking rule entities: list, get, create, update, delete. Use items for all create, update, and delete operations; one item selects a single operation and multiple items select a batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Parking rule management request")] ParkingRuleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case ParkingRuleAction.List:
                return await client.ListEntities("ParkingRule", request.PageSize, request.Page);
            case ParkingRuleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for get.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "ParkingRule");
            case ParkingRuleAction.Create:
            {
                RequireItems(request);
                var names = ManagementBatchItemHelper.GetCreateNames(request.Items!);
                if (request.Items!.Any(item => item?.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var items = request.Items!.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "ParkingRule", items);
            }
            case ParkingRuleAction.Update:
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
                object GetBody(ParkingRuleBatchItem item) => item.Body is { } properties
                    ? properties with { Name = properties.Name is null ? null : EntityNameSanitizer.Sanitize(properties.Name) }
                    : new { Name = EntityNameSanitizer.Sanitize(item.Name!) };
                if (request.Items.Count == 1)
                {
                    var item = request.Items[0];
                    await client.RequireEntityType(item.Id!.Value, "items", "ParkingRule");
                    await RequireBodyTypes(client, item.Body);
                    return await client.UpdateEntity(item.Id.Value.ToString(), GetBody(item));
                }
                var updates = request.Items.Select(item => (item.Id!.Value, GetBody(item))).ToList();
                return await client.UpdateEntitiesBatch(updates, "ParkingRule",
                    index => RequireBodyTypes(client, request.Items[index].Body));
            }
            case ParkingRuleAction.Delete:
                RequireItems(request);
                if (request.Items!.Any(item => item?.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                return await DeleteToolHelper.Execute(client, ManagementBatchItemHelper.GetDeleteIds(request.Items), "ParkingRule");
            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

    private static void RequireItems(ParkingRuleManagementRequest request)
    {
        if (request.Id is not null)
            throw new ArgumentException("Parameter 'id' is only valid for get; use 'items' for create, update, and delete.", "id");
        if (request.Items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create, update, and delete.", "items");
    }

    private static async Task RequireBodyTypes(SecurityCenterRestClient client, ParkingRuleUpdateProperties? body)
    {
        if (body is null) return;
        if (body.Permit is { } permitId)
            await client.RequireEntityType(permitId, "items.body.permit", "Permit");
    }
}

public sealed record ParkingRuleBatchItem(
    [property: Description("ParkingRule name for create or rename"), MinLength(1)] string? Name = null,
    [property: Description("ParkingRule GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed ParkingRule properties for update")] ParkingRuleUpdateProperties? Body = null) : IManagementBatchItem;

public enum ParkingRuleAction { Get, List, Create, Update, Delete }

public sealed record ParkingRuleManagementRequest(
    [property: Description("Action to perform"), Required] ParkingRuleAction Action,
    [property: Description("ParkingRule GUID for get")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ParkingRuleBatchItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record ParkingRuleUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Convenience time")] TimeSpan? ConvenienceTime = null,
    [Description("Default expiration delay")] TimeSpan? DefaultExpirationDelay = null,
    [Description("Grace period")] TimeSpan? GracePeriod = null,
    [Description("Whether pay-by-plate synchronization is supported")] bool? PayByPlateSyncSupport = null,
    [Description("Permit GUID")] Guid? Permit = null);
