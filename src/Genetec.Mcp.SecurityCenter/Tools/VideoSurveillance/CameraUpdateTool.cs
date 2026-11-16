using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraUpdateTool
{
    [McpServerTool(
        Name = "camera_update",
        Title = "Update Camera",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more cameras in Security Center. " +
        "Provide a single item to update one camera, or multiple items for a batch update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera update request")] CameraUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Camera");
            return await client.UpdateEntity(item.Id.ToString(), item);
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)i))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Camera");
    }
}

public sealed record CameraUpdateRequest(
    [property: Description("One or more cameras to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CameraUpdateItem> Items);

public sealed record CameraUpdateItem(
    [property: Description("Camera GUID"), Required] Guid Id,
    [Description("New camera name")] string? Name = null);
