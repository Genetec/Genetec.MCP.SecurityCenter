using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class AlarmGetTool
{
    public sealed record AlarmGetRequest(
        [property: Description("Alarm GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "alarm_get",
        Title = "Get Alarm",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Retrieve full details of a single alarm definition in Security Center by GUID.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Alarm get request")] AlarmGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "Alarm");
    }
}
