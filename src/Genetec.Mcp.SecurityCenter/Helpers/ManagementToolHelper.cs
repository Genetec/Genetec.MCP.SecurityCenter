using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.Helpers;

public interface IManagementCrudItem<out TBody> : IManagementBatchItem where TBody : class
{
    TBody? Body { get; }
}

public static class ManagementToolHelper
{
    public static string? SanitizeUpdateName(string? name, string parameter = "items.body.name")
    {
        if (name is null) return null;
        var sanitized = EntityNameSanitizer.Sanitize(name);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Entity name must contain nonempty supported characters.", parameter);
        return sanitized;
    }

    public static List<T> RequireItems<T>(List<T>? items)
    {
        if (items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create, update, and delete.", "items");
        return items;
    }

    public static async Task<object> Update<TBody>(
        SecurityCenterRestClient client,
        IReadOnlyList<IManagementCrudItem<TBody>> items,
        string expectedType,
        Func<TBody, TBody> sanitizeBody,
        Func<string, TBody>? renameBody = null,
        Func<int, Task>? validateItem = null) where TBody : class
    {
        var bodies = items.Select(item =>
        {
            if (item is null || item.Id is null)
                throw new ArgumentException("Each item must include an id for update.", "items");
            if (item.Body is not null && item.Name is not null)
                throw new ArgumentException("Provide either 'items.body' or 'items.name', not both.", "items");
            if (item.Body is { } body) return (object)sanitizeBody(body);
            if (renameBody is not null && !string.IsNullOrWhiteSpace(item.Name))
                return renameBody(SanitizeUpdateName(item.Name, "items.name")!);
            throw new ArgumentException(renameBody is null
                ? "Each item must include a body for update."
                : "Each item must include a body or nonempty name for update.", "items");
        }).ToList();

        if (items.Count == 1)
        {
            await client.RequireEntityType(items[0].Id!.Value, "items", expectedType);
            if (validateItem is not null) await validateItem(0);
            return await client.UpdateEntity(items[0].Id!.Value.ToString(), bodies[0]);
        }
        var updates = items.Select((item, index) => (item.Id!.Value, bodies[index])).ToList();
        return await client.UpdateEntitiesBatch(updates, expectedType, validateItem);
    }

    public static List<Guid> DeleteIds<TBody>(IReadOnlyList<IManagementCrudItem<TBody>> items) where TBody : class
    {
        if (items.Any(item => item?.Body is not null))
            throw new ArgumentException("Parameter 'items.body' is not valid for delete.", "items.body");
        return ManagementBatchItemHelper.GetDeleteIds(items);
    }
}
