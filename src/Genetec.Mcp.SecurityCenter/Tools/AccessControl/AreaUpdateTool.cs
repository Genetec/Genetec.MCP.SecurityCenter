using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaUpdateTool
{
    [McpServerTool(
        Name = "area_update",
        Title = "Update Area",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more areas in Security Center. " +
        "Provide a single item to rename one area, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area update request")] AreaUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Area");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Area");
    }
}

public sealed record AreaUpdateRequest(
    [property: Description("One or more areas to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AreaUpdateItem> Items);

public sealed record AreaUpdateItem(
    [property: Description("Area GUID"), Required] Guid Id,
    [property: Description("New area name"), Required, MinLength(1)] string Name);
