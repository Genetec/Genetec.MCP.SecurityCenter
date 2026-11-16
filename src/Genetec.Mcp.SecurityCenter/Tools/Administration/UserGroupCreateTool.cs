using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGroupCreateTool
{
    [McpServerTool(
        Name = "user_group_create",
        Title = "Create User Group",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more user groups in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group create request")] UserGroupCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>("UserGroup", EntityNameSanitizer.Sanitize(item.Name));
        }

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(item.Name)))
            .ToList();

        var batchRaw = await client.CreateEntitiesBatch("UserGroup", batchItems);
        return CreateToolHelper.BuildBatchResponse(batchRaw, batchItems.Count);
    }
}

public sealed record UserGroupCreateRequest(
    [property: Description("One or more user groups to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<UserGroupItem> Items);

public sealed record UserGroupItem(
    [property: Description("User group name"), Required, MinLength(1)] string Name);
