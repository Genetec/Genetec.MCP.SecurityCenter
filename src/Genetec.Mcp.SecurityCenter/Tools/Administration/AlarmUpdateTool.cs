using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class AlarmUpdateTool
{
    [McpServerTool(Name = "alarm_update", Title = "Update Alarm", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true),
     Description("Update one or more alarm definitions in Security Center. Provide a single item to rename one alarm, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm update request")] AlarmUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Alarm");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Alarm");
    }
}

public sealed record AlarmUpdateRequest(
    [property: Description("One or more alarms to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AlarmUpdateItem> Items);

public sealed record AlarmUpdateItem(
    [property: Description("Alarm GUID"), Required] Guid Id,
    [property: Description("New alarm name"), Required, MinLength(1)] string Name);
