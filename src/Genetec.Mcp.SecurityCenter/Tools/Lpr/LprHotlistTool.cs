using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class LprHotlistTool
{
    [McpServerTool(
        Name = "lpr_hotlist",
        Title = "LPR Hotlist Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage LPR hotlists: get, list, create, update, delete. " +
                 "Create supports single (name) or batch (items).")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR hotlist management request")] LprHotlistManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case LprHotlistAction.List:
                return await client.ListEntities("HotlistRule", request.PageSize, request.Page);

            case LprHotlistAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "HotlistRule");

            case LprHotlistAction.Create:
                return await Create(client, request.Name, request.Items);

            case LprHotlistAction.Update:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for update.");
                if (request.Update is not null)
                {
                    await client.RequireEntityType(request.Id.Value, "id", "HotlistRule");
                    return await client.UpdateEntity(request.Id!.ToString(), request.Update);
                }
                else if (request.Name is not null)
                {
                    var sanitizedName = EntityNameSanitizer.Sanitize(request.Name);
                    if (string.IsNullOrWhiteSpace(sanitizedName))
                        throw new ArgumentException("Parameter 'name' must not be empty or whitespace for update.");
                    await client.RequireEntityType(request.Id.Value, "id", "HotlistRule");
                    return await client.UpdateEntity(request.Id!.ToString(), new { Name = sanitizedName });
                }
                else
                    throw new ArgumentException("Parameter 'update' or 'name' is required for update.");

            case LprHotlistAction.Delete:
                var deleteItems = request.Items is { Count: > 0 }
                    ? ManagementBatchItemHelper.GetDeleteIds(request.Items)
                    : request.Id.HasValue
                        ? [request.Id.Value]
                        : throw new ArgumentException("Parameter 'id' or 'items' is required for delete.");
                return await DeleteToolHelper.Execute(client, deleteItems, "HotlistRule");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static async Task<object> Create(SecurityCenterRestClient client, string? name, List<LprHotlistBatchItem>? items)
    {
        if (items is { Count: > 0 })
        {
            if (name is not null)
                throw new ArgumentException("Provide either 'name' or 'items', not both.");

            var names = ManagementBatchItemHelper.GetCreateNames(items);
            var batchItems = names
                .Select(n => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(n)))
                .ToList();
            var raw = await client.CreateEntitiesBatch("HotlistRule", batchItems);
            return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
        }

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter 'name' is required in single mode.");

        return await client.CreateEntity<EntityCreateResult>("HotlistRule", EntityNameSanitizer.Sanitize(name));
    }


}

public enum LprHotlistAction
{
    Get, List, Create, Update, Delete,
}

public sealed record LprHotlistManagementRequest(
    [property: Description("Action to perform"), Required] LprHotlistAction Action,
    [property: Description("Hotlist GUID")] Guid? Id = null,
    [Description("Name (single mode for create)")] string? Name = null,
    [property: Description("For batch create, each item has a name; for batch delete, each item has an id"), MaxLength(ResourceLimits.MaxBatchItems)] List<LprHotlistBatchItem>? Items = null,
    [Description("Typed hotlist properties for update")] LprHotlistUpdateRequest? Update = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record LprHotlistBatchItem(
    [property: Description("Hotlist name for create")] string? Name = null,
    [property: Description("Hotlist GUID for delete")] Guid? Id = null) : IManagementBatchItem;

public sealed record LprHotlistUpdateRequest(
    [Description("New hotlist name")] string? Name = null,
    [Description("Whether to periodically transfer the hotlist to patrollers")] bool? AllowPeriodicTransfer = null,
    [Description("Hotlist attribute delimiter")] string? AttributeDelimiter = null,
    [Description("Hit color in ARGB hex format")] string? Color = null,
    [Description("Hotlist content file path")] string? ContentFilePath = null,
    [Description("Email address for hotlist hits")] string? EmailAddress = null,
    [Description("Whether the hotlist can be edited in Security Desk")] bool? EnableTextEditor = null,
    [Description("Hit priority from 0 (highest) to 100"), Range(0, 100)] uint? HitPriority = null,
    [Description("Whether the hotlist is covert")] bool? IsCovert = null,
    [Description("Whether to override patroller privacy settings")] bool? ServerSidePrivacy = null,
    [Description("Patroller sound file path for hits")] string? SoundFilePath = null,
    [Description("Whether to transfer the hotlist immediately when modified")] bool? TransferOnModification = null,
    [Description("Whether plate matching uses wildcard characters")] bool? UseWildcards = null);
