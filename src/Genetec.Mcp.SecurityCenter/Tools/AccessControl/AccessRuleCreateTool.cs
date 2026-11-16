using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessRuleCreateTool
{
    [McpServerTool(
        Name = "access_rule_create",
        Title = "Create Access Rule",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more access rules in Security Center. " +
        "For permanent rules provide name and optional schedule. " +
        "For temporary rules provide name, start_time, and end_time (both required together). " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access rule create request")] AccessRuleCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        foreach (var item in request.Items)
        {
            if (item.StartTime is null && item.EndTime is not null)
                throw new ArgumentException("Parameter 'start_time' is required when 'end_time' is provided.");
            if (item.StartTime is not null && item.EndTime is null)
                throw new ArgumentException("Parameter 'end_time' is required when 'start_time' is provided.");
        }

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>(
                item.StartTime is null ? "AccessRule" : "AccessRule,Temporary",
                EntityNameSanitizer.Sanitize(item.Name),
                item);
        }

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name),
                item,
                item.StartTime is null ? "AccessRule" : "AccessRule,Temporary"))
            .ToList();

        var raw = await client.CreateEntitiesBatch("AccessRule", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }

}

public sealed record AccessRuleCreateRequest(
    [property: Description("One or more access rules to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AccessRuleItem> Items);

public sealed record AccessRuleItem(
    [property: Description("Access rule name"), Required, MinLength(1)] string Name,
    [property: Description("Schedule GUID for permanent rule")] Guid? Schedule = null,
    [property: Description("Start time for temporary rule (ISO 8601)"), Iso8601] string? StartTime = null,
    [property: Description("End time for temporary rule (ISO 8601)"), Iso8601] string? EndTime = null);
