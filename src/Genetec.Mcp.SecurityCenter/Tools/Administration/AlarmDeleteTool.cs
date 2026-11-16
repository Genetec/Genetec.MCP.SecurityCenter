using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class AlarmDeleteTool
{
    [McpServerTool(Name = "alarm_delete", Title = "Delete Alarm", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Permanently delete one or more alarm definitions from Security Center. Provide a single item to delete one alarm, or multiple items for a batch delete.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm delete request")] AlarmDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "Alarm");
    }
}

public sealed record AlarmDeleteRequest(
    [property: Description("One or more alarm GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
