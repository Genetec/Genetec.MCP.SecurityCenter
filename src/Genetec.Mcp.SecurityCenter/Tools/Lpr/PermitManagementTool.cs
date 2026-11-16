using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class PermitManagementTool
{
    [McpServerTool(
        Name = "permit_management",
        Title = "Permit Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage permits: get, list, create, update, delete. Create supports single (name) or batch (items).")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Permit management request")] PermitManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case PermitAction.List:
                return await client.ListEntities("Permit", request.PageSize, request.Page);

            case PermitAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "Permit");

            case PermitAction.Create:
                return await Create(client, request.Name, request.Items);

            case PermitAction.Update:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for update.");
                if (request.Body is not null)
                {
                    await client.RequireEntityType(request.Id.Value, "id", "Permit");
                    return await client.UpdateEntity(request.Id!.ToString(), request.Body);
                }
                else if (request.Name is not null)
                {
                    var sanitizedName = EntityNameSanitizer.Sanitize(request.Name);
                    if (string.IsNullOrWhiteSpace(sanitizedName))
                        throw new ArgumentException("Parameter 'name' must not be empty or whitespace for update.");
                    await client.RequireEntityType(request.Id.Value, "id", "Permit");
                    return await client.UpdateEntity(request.Id!.ToString(), new { Name = sanitizedName });
                }
                else
                    throw new ArgumentException("Parameter 'body' or 'name' is required for update.");

            case PermitAction.Delete:
                var deleteItems = request.Items is { Count: > 0 }
                    ? ManagementBatchItemHelper.GetDeleteIds(request.Items)
                    : request.Id.HasValue
                        ? [request.Id.Value]
                        : throw new ArgumentException("Parameter 'id' or 'items' is required for delete.");
                return await DeleteToolHelper.Execute(client, deleteItems, "Permit");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static async Task<object> Create(SecurityCenterRestClient client, string? name, List<PermitBatchItem>? items)
    {
        if (items is { Count: > 0 })
        {
            if (name is not null)
                throw new ArgumentException("Provide either 'name' or 'items', not both.");

            var names = ManagementBatchItemHelper.GetCreateNames(items);
            var batchItems = names
                .Select(n => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(n)))
                .ToList();
            var raw = await client.CreateEntitiesBatch("Permit", batchItems);
            return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
        }

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter 'name' is required in single mode.");

        return await client.CreateEntity<EntityCreateResult>("Permit", EntityNameSanitizer.Sanitize(name));
    }

}

public enum PermitAction { Get, List, Create, Update, Delete }

public sealed record PermitManagementRequest(
    [property: Description("Action to perform"), Required] PermitAction Action,
    [property: Description("Permit GUID")] Guid? Id = null,
    [Description("Name (single mode for create)")] string? Name = null,
    [property: Description("For batch create, each item has a name; for batch delete, each item has an id"), MaxLength(ResourceLimits.MaxBatchItems)] List<PermitBatchItem>? Items = null,
    [Description("Typed permit properties to update")] PermitUpdateProperties? Body = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record PermitBatchItem(
    [property: Description("Permit name for create")] string? Name = null,
    [property: Description("Permit GUID for delete")] Guid? Id = null) : IManagementBatchItem;

public sealed record PermitUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Permit color")] string? Color = null,
    [Description("Download content only at logon")] bool? DownloadOnlyAtLogon = null,
    [Description("Transfer permit content immediately")] bool? ExpeditiveTransfer = null);
