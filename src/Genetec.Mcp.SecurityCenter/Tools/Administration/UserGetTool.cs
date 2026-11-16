using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGetTool
{
    public sealed record UserGetRequest(
        [property: Description("User GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "user_get",
        Title = "Get User",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single user in Security Center by GUID, " +
        "including name, status, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User get request")] UserGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "User");
    }
}
