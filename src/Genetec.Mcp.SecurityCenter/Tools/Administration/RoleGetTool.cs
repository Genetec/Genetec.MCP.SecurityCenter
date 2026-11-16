using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class RoleGetTool
{
    public sealed record RoleGetRequest(
        [property: Description("Role GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "role_get",
        Title = "Get Role",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single role in Security Center by GUID, " +
        "including name, type, and properties.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Role get request")] RoleGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "Role");
    }
}
