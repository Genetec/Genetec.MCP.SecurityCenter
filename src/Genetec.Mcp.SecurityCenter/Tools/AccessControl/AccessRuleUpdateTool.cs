using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessRuleUpdateTool
{
    [McpServerTool(
        Name = "access_rule_update",
        Title = "Update Access Rule",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more access rules in Security Center. " +
        "Provide a single item to rename one rule, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access rule update request")] AccessRuleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "AccessRule");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "AccessRule");
    }
}

public sealed record AccessRuleUpdateRequest(
    [property: Description("One or more access rules to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AccessRuleUpdateItem> Items);

public sealed record AccessRuleUpdateItem(
    [property: Description("Access rule GUID"), Required] Guid Id,
    [property: Description("New access rule name"), Required, MinLength(1)] string Name);
