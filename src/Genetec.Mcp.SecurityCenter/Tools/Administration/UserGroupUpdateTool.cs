using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGroupUpdateTool
{
    [McpServerTool(
        Name = "user_group_update",
        Title = "Update User Group",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Update one or more user group names in Security Center. " +
        "Provide a single item for a direct update, or multiple items for a batch update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group update request")] UserGroupUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "UserGroup");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = item.Name });
        }

        var pairs = request.Items
            .Select(item => (item.Id, (object)new { Name = item.Name }))
            .ToList();

        return await client.UpdateEntitiesBatch(pairs, "UserGroup");
    }
}

public sealed record UserGroupUpdateRequest(
    [property: Description("One or more user groups to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<UserGroupUpdateItem> Items);

public sealed record UserGroupUpdateItem(
    [property: Description("User group GUID"), Required] Guid Id,
    [Description("New name")] string? Name = null);
