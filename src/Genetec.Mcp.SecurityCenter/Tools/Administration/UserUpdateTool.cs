using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserUpdateTool
{
    [McpServerTool(Name = "user_update", Title = "Update User", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true),
     Description("Update one or more users in Security Center including name, first name, and last name. Provide a single item to update one user, or multiple items for a batch update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User update request")] UserUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "User");
            return await client.UpdateEntity(item.Id.ToString(), item);
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)i))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "User");
    }
}

public sealed record UserUpdateRequest(
    [property: Description("One or more users to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<UserUpdateItem> Items);

public sealed record UserUpdateItem(
    [property: Description("User GUID"), Required] Guid Id,
    [Description("Username")] string? Name = null,
    [Description("First name")] string? FirstName = null,
    [Description("Last name")] string? LastName = null);
