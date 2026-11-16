using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class DoorGetTool
{
    public sealed record DoorGetRequest(
        [property: Description("Door GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "door_get",
        Title = "Get Door",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single door in Security Center by GUID, " +
        "including name, entity type, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door get request")] DoorGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "Door");
    }
}
