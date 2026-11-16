using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserDeleteTool
{
    [McpServerTool(Name = "user_delete", Title = "Delete User", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Permanently delete one or more users from Security Center. Provide a single item to delete one user, or multiple items for a batch delete.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User delete request")] UserDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "User");
    }
}

public sealed record UserDeleteRequest(
    [property: Description("One or more user GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
