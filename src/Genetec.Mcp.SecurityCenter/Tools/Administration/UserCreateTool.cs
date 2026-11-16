using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserCreateTool
{
    [McpServerTool(
        Name = "user_create",
        Title = "Create User",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more users in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User create request")] UserCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>("User", EntityNameSanitizer.Sanitize(item.Name),
                item.FirstName is null && item.LastName is null ? null : new { item.FirstName, item.LastName });
        }

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name),
                item.FirstName is null && item.LastName is null ? null : new { item.FirstName, item.LastName }))
            .ToList();

        var batchRaw = await client.CreateEntitiesBatch("User", batchItems);
        return CreateToolHelper.BuildBatchResponse(batchRaw, batchItems.Count);
    }
}

public sealed record UserCreateRequest(
    [property: Description("One or more users to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<UserItem> Items);

public sealed record UserItem(
    [property: Description("Username"), Required, MinLength(1)] string Name,
    [Description("First name")] string? FirstName = null,
    [Description("Last name")] string? LastName = null);
