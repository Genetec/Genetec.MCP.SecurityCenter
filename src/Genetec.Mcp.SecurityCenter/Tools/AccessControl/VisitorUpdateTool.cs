using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorUpdateTool
{
    [McpServerTool(
        Name = "visitor_update",
        Title = "Update Visitor",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more visitors in Security Center. " +
        "Provide a single item to rename one visitor, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor update request")] VisitorUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Visitor");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Visitor");
    }
}

public sealed record VisitorUpdateRequest(
    [property: Description("One or more visitors to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<VisitorUpdateItem> Items);

public sealed record VisitorUpdateItem(
    [property: Description("Visitor GUID"), Required] Guid Id,
    [property: Description("New visitor name"), Required, MinLength(1)] string Name);
