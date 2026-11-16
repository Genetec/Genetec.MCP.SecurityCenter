using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class AlarmCreateTool
{
    [McpServerTool(Name = "alarm_create", Title = "Create Alarm", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true),
     Description("Create one or more alarm definitions in Security Center. Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm create request")] AlarmCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>("Alarm", EntityNameSanitizer.Sanitize(item.Name));
        }
        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(item.Name)))
            .ToList();
        var batchRaw = await client.CreateEntitiesBatch("Alarm", batchItems);
        return CreateToolHelper.BuildBatchResponse(batchRaw, batchItems.Count);
    }
}

public sealed record AlarmCreateRequest(
    [property: Description("One or more alarms to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AlarmItem> Items);

public sealed record AlarmItem(
    [property: Description("Alarm name"), Required, MinLength(1)] string Name);
