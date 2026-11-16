using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class DoorCreateTool
{
    [McpServerTool(
        Name = "door_create",
        Title = "Create Door",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Create one or more door entities in Security Center. " +
        "Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door create request")] DoorCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
            return await client.CreateEntity<EntityCreateResult>(
                "Door", EntityNameSanitizer.Sanitize(request.Items[0].Name));

        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(
                EntityNameSanitizer.Sanitize(item.Name)))
            .ToList();

        var raw = await client.CreateEntitiesBatch("Door", batchItems);
        return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
    }
}

public sealed record DoorCreateRequest(
    [property: Description("One or more doors to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<DoorItem> Items);

public sealed record DoorItem(
    [property: Description("Door name"), Required, MinLength(1)] string Name);
