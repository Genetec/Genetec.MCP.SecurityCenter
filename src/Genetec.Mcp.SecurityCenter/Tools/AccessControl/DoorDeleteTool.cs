using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class DoorDeleteTool
{
    [McpServerTool(
        Name = "door_delete",
        Title = "Delete Door",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Permanently delete one or more doors from Security Center. " +
        "Provide a single GUID or multiple GUIDs for batch deletion.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door delete request")] DoorDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "Door");
    }
}

public sealed record DoorDeleteRequest(
    [property: Description("One or more door GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
