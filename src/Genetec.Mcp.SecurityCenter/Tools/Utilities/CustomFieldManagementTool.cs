using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class CustomFieldManagementTool
{
    [McpServerTool(
        Name = "custom_field_management",
        Title = "Custom Field Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
         "Manage custom fields: get_value, set_value, create_field, delete_field. " +
         "Batch mode (set_value/get_value): provide items with entity_id, field_name, and (for set_value) value.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Custom field management request")] CustomFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CustomFieldAction.SetValue:
                if (request.Items is { Count: > 0 })
                {
                    if (request.EntityId is not null || request.FieldName is not null || request.Value is not null)
                        throw new ArgumentException("Provide either 'entity_id'/'field_name'/'value' or 'items', not both.");
                    foreach (var item in request.Items)
                        if (item.Value is null)
                            throw new ArgumentException($"Batch item for entity '{item.EntityId}' is missing 'value'.", "value");
                    return await ExecuteBatch(client, request.Items, setValue: true);
                }
                if (request.EntityId is null) throw new ArgumentException("Parameter 'entity_id' is required for set_value.");
                if (string.IsNullOrWhiteSpace(request.FieldName)) throw new ArgumentException("Parameter 'field_name' is required.");
                return await client.SetCustomFieldValue(request.EntityId!.ToString(), request.FieldName, request.Value!);

            case CustomFieldAction.GetValue:
                if (request.Items is { Count: > 0 })
                {
                    if (request.EntityId is not null || request.FieldName is not null)
                        throw new ArgumentException("Provide either 'entity_id'/'field_name' or 'items', not both.");
                    return await ExecuteBatch(client, request.Items, setValue: false);
                }
                if (request.EntityId is null) throw new ArgumentException("Parameter 'entity_id' is required for get_value.");
                if (string.IsNullOrWhiteSpace(request.FieldName)) throw new ArgumentException("Parameter 'field_name' is required.");
                return await client.GetCustomFieldValue(request.EntityId!.ToString(), request.FieldName);

            case CustomFieldAction.CreateField:
                if (request.EntityType is null) throw new ArgumentException("Parameter 'entity_type' is required for create_field.");
                if (request.DataType is null) throw new ArgumentException("Parameter 'data_type' is required for create_field.");
                if (string.IsNullOrWhiteSpace(request.FieldName)) throw new ArgumentException("Parameter 'field_name' is required.");
                return await client.CreateCustomField(request.EntityType.Value, request.FieldName, request.DataType.Value, request.DefaultValue);

            case CustomFieldAction.DeleteField:
                if (request.EntityType is null) throw new ArgumentException("Parameter 'entity_type' is required for delete_field.");
                if (string.IsNullOrWhiteSpace(request.FieldName)) throw new ArgumentException("Parameter 'field_name' is required.");
                return await client.DeleteCustomField(request.EntityType.Value, request.FieldName);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static async Task<CustomFieldBatchResult> ExecuteBatch(
        SecurityCenterRestClient client, List<CustomFieldBatchItem> items, bool setValue)
    {
        ResourceLimits.EnsureBatchOperationCount(items.Count, "items");
        var tasks = items.Select(async (item, index) =>
        {
            var key = new CustomFieldBatchKey(item.EntityId.ToString(), item.FieldName);
            try
            {
                var response = setValue
                    ? await client.SetCustomFieldValue(item.EntityId.ToString(), item.FieldName, item.Value!)
                    : await client.GetCustomFieldValue(item.EntityId.ToString(), item.FieldName);
                return new CustomFieldBatchResultItem(index, key, true, response);
            }
            catch (Exception ex)
            {
                return new CustomFieldBatchResultItem(index, key, false, null, ex.Message);
            }
        }).ToArray();
        var results = (await Task.WhenAll(tasks)).ToList();
        var successCount = results.Count(item => item.Success);
        return new CustomFieldBatchResult("batch", results.Count, successCount, results.Count - successCount, results);
    }
}

public sealed record CustomFieldBatchKey(string EntityId, string FieldName);
public sealed record CustomFieldBatchResultItem(int Index, CustomFieldBatchKey Key, bool Success, string? Result, string? Error = null);
public sealed record CustomFieldBatchResult(string Mode, int RequestedCount, int SuccessCount, int FailureCount, List<CustomFieldBatchResultItem> Items);

public enum CustomFieldAction { GetValue, SetValue, CreateField, DeleteField }

public sealed record CustomFieldRequest(
    [property: Description("Action to perform"), Required] CustomFieldAction Action,
    [property: Description("Entity GUID (for get_value, set_value single mode)")] Guid? EntityId = null,
    [Description("Custom field name")] string? FieldName = null,
    [Description("Value to set (for set_value single mode)")] string? Value = null,
    [Description("Entity type (for create_field, delete_field)")] CustomFieldEntityType? EntityType = null,
    [Description("Data type (for create_field)")] CustomFieldDataType? DataType = null,
    [Description("Default value (for create_field)")] string? DefaultValue = null,
    [property: Description("Batch items (for get_value/set_value batch mode)"), MaxLength(ResourceLimits.MaxBatchItems)] List<CustomFieldBatchItem>? Items = null);

public sealed record CustomFieldBatchItem(
    [property: Description("Entity GUID"), Required] Guid EntityId,
    [property: Description("Custom field name"), Required, MinLength(1)] string FieldName,
    [Description("Value to set (for set_value)")] string? Value = null);
