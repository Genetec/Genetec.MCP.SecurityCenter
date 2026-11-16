using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessRuleDeleteTool
{
    [McpServerTool(
        Name = "access_rule_delete",
        Title = "Delete Access Rule",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Permanently delete one or more access rules from Security Center. " +
        "Provide a single GUID or multiple GUIDs for batch deletion.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access rule delete request")] AccessRuleDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "AccessRule");
    }
}

public sealed record AccessRuleDeleteRequest(
    [property: Description("One or more access rule GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
