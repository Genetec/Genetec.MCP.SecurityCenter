using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGroupGetTool
{
    public sealed record UserGroupGetRequest(
        [property: Description("User group GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "user_group_get",
        Title = "Get User Group",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Retrieve full details of a single user group in Security Center by GUID.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group get request")] UserGroupGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "UserGroup");
    }
}
