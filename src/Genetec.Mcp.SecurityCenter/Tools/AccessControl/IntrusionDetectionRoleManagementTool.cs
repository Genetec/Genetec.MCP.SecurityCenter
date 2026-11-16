using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class IntrusionDetectionRoleManagementTool
{
    [McpServerTool(
        Name = "intrusion_detection_role_management",
        Title = "Intrusion Detection Role Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage intrusion detection roles: list, get, and create")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Intrusion detection role management request")] IntrusionDetectionRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case IntrusionDetectionRoleAction.List:
                return await client.ListEntities("Role", request.PageSize, request.Page);

            case IntrusionDetectionRoleAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "Role");
                return await client.GetEntityTrimmed(request.Id!.ToString());

            case IntrusionDetectionRoleAction.Create:
                return await CreateRole(client, request.Items);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

    private static async Task<object> CreateRole(
        SecurityCenterRestClient client,
        List<string>? items)
    {
        if (items is null or { Count: 0 })
            throw new ArgumentException("Parameter 'items' is required for create.");

        if (items.Count == 1)
        {
            var sanitizedName = EntityNameSanitizer.Sanitize(items[0]);
            if (string.IsNullOrWhiteSpace(sanitizedName))
                throw new ArgumentException("Parameter 'items[0]' is invalid after sanitization.");
            return await client.CreateEntity<EntityCreateResult>("Role", sanitizedName,
                new { Type = "IntrusionDetection" });
        }

        var batchItems = items
            .Select(n => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(n),
                new { Type = "IntrusionDetection" }))
            .ToList();

        var raw = await client.CreateEntitiesBatch("Role", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }
}

public enum IntrusionDetectionRoleAction
{
    Get,
    List,
    Create,
}

public sealed record IntrusionDetectionRoleRequest(
    [property: Description("Action to perform"), Required] IntrusionDetectionRoleAction Action,
    [property: Description("Role GUID")] Guid? Id = null,
    [property: Description("Role names for create (count 1 = single, 2+ = batch)"), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);
