using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorCreateTool
{
    [McpServerTool(
        Name = "visitor_create",
        Title = "Create Visitor",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more visitor entities in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor create request")] VisitorCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
            return await client.CreateEntity<EntityCreateResult>(
                "Visitor", EntityNameSanitizer.Sanitize(request.Items[0].Name));

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name)))
            .ToList();

        var raw = await client.CreateEntitiesBatch("Visitor", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }
}

public sealed record VisitorCreateRequest(
    [property: Description("One or more visitors to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<VisitorItem> Items);

public sealed record VisitorItem(
    [property: Description("Visitor name"), Required, MinLength(1)] string Name);
