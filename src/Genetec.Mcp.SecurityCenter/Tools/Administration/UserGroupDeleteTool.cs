using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGroupDeleteTool
{
    [McpServerTool(
        Name = "user_group_delete",
        Title = "Delete User Group",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Permanently delete one or more user groups from Security Center. " +
        "Provide a single item for a direct delete, or multiple items for a batch delete.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group delete request")] UserGroupDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "UserGroup");
    }
}

public sealed record UserGroupDeleteRequest(
    [property: Description("One or more user group GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
