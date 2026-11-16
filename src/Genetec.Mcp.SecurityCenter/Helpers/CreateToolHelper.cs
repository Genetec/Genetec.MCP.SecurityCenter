using System.Text.Json;
using System.Text.RegularExpressions;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.Helpers;

public static class CreateToolHelper
{
    internal static readonly Regex GuidRegex = new(@"{?[0-9a-fA-F]{8}(?:-?[0-9a-fA-F]{4}){3}-?[0-9a-fA-F]{12}}?");
    private static readonly JsonSerializerOptions SnakeCaseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static async Task<object> Execute(
        SecurityCenterRestClient client,
        string entityType,
        IReadOnlyList<SecurityCenterRestClient.BatchCreateItem> items,
        Func<int, Task>? validateItem = null)
    {
        if (items.Count == 0)
            throw new ArgumentException("Batch create requires at least one item.", "items");
        ResourceLimits.EnsureBatchOperationCount(items.Count, "items");
        async Task<EntityCreateResult> CreateItem(int index)
        {
            if (validateItem is not null) await validateItem(index);
            var item = items[index];
            return await client.CreateEntity<EntityCreateResult>(item.EntityType ?? entityType, item.Name, item.Body, item.InitializationMethod);
        }

        if (items.Count == 1) return await CreateItem(0);
        if (validateItem is null)
            return BuildBatchResponse(await client.CreateEntitiesBatch(entityType, items), items.Count);

        var results = new List<BatchCreateResultItem>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            try
            {
                var result = await CreateItem(index);
                results.Add(new BatchCreateResultItem(index, true, Guid: result.Guid));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                results.Add(new BatchCreateResultItem(index, false, Error: error.Message));
            }
        }
        var successCount = results.Count(result => result.Success);
        return new BatchCreateResult("batch", items.Count, successCount, items.Count - successCount, results);
    }

    public static BatchCreateResult BuildBatchResponse(string rawResult, int requestedCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResult);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.True)
            {
                return new BatchCreateResult("batch", requestedCount, 0, requestedCount,
                    Enumerable.Range(0, requestedCount)
                        .Select(i => new BatchCreateResultItem(i, false, Error: rawResult))
                        .ToList());
            }
        }
        catch
        {
            // Fall through to GUID extraction for non-standard response envelopes.
        }

        var guids = GuidRegex.Matches(rawResult)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = Enumerable.Range(0, requestedCount)
            .Select(i => i < guids.Count
                ? new BatchCreateResultItem(i, true, Guid: Guid.Parse(guids[i]))
                : new BatchCreateResultItem(i, false, Error: "No GUID returned for this item."))
            .ToList();

        return new BatchCreateResult("batch", requestedCount, guids.Count,
            Math.Max(0, requestedCount - guids.Count), results);
    }

    public static async Task<object> CreateRole(
        SecurityCenterRestClient client,
        string roleType,
        List<string>? items,
        Guid? serverId = null)
    {
        if (items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create.", "items");
        ResourceLimits.EnsureBatchOperationCount(items.Count, "items");
        var names = items.Select(EntityNameSanitizer.Sanitize).ToList();
        if (names.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Parameter 'items' must contain nonempty role names.", "items");
        var props = new { Type = roleType };
        var initializationMethod = serverId is { } id ? $"AddServer({id})" : null;
        if (items.Count == 1)
            return await client.CreateEntity<EntityCreateResult>("Role", names[0], props, initializationMethod);

        var batchItems = names
            .Select(name => new SecurityCenterRestClient.BatchCreateItem(name, props,
                InitializationMethod: initializationMethod))
            .ToList();
        return BuildBatchResponse(await client.CreateEntitiesBatch("Role", batchItems), batchItems.Count);
    }
}
