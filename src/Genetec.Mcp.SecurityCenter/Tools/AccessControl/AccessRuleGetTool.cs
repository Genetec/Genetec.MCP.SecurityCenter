using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessRuleGetTool
{
    public sealed record AccessRuleGetRequest(
        [property: Description("Access rule GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "access_rule_get",
        Title = "Get Access Rule",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single access rule in Security Center by GUID, " +
        "including name, schedule, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access rule get request")] AccessRuleGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "AccessRule");
    }
}
