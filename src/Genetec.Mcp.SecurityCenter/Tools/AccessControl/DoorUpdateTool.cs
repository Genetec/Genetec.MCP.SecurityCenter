using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class DoorUpdateTool
{
    [McpServerTool(
        Name = "door_update",
        Title = "Update Door",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more doors in Security Center. " +
        "Provide a single item to rename one door, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door update request")] DoorUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Door");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Door");
    }
}

public sealed record DoorUpdateRequest(
    [property: Description("One or more doors to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<DoorUpdateItem> Items);

public sealed record DoorUpdateItem(
    [property: Description("Door GUID"), Required] Guid Id,
    [property: Description("New door name"), Required, MinLength(1)] string Name);
