using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaCreateTool
{
    [McpServerTool(
        Name = "area_create",
        Title = "Create Area",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more areas in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area create request")] AreaCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
            return await client.CreateEntity<EntityCreateResult>(
                "Area", EntityNameSanitizer.Sanitize(request.Items[0].Name));

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name)))
            .ToList();

        var raw = await client.CreateEntitiesBatch("Area", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }
}

public sealed record AreaCreateRequest(
    [property: Description("One or more areas to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AreaItem> Items);

public sealed record AreaItem(
    [property: Description("Area name"), Required, MinLength(1)] string Name);
