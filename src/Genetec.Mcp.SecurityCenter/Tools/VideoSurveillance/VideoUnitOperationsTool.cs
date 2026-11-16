using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

public enum VideoUnitOperationsAction
{
    Move,
    Reconnect,
    Reboot,
    DeleteGhostCamera
}

public sealed record VideoUnitOperationsRequest(
    [property: Description("Action to perform"), Required] VideoUnitOperationsAction Action,
    [property: Description("Video unit GUID")] Guid? Id = null,
    [property: Description("Destination archiver GUID (for move)")] Guid? TargetArchiverId = null,
    [property: Description("Ghost camera GUID (for delete_ghost_camera)")] Guid? CameraId = null);

[McpServerToolType]
public class VideoUnitOperationsTool
{
    [McpServerTool(
        Name = "video_unit_operations",
        Title = "Video Unit Operations",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Execute operational commands on video units. " +
        "Use when moving units between archivers, reconnecting, rebooting, or deleting ghost cameras. " +
        "Actions: move, reconnect, reboot, delete_ghost_camera")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Video unit operation request")] VideoUnitOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case VideoUnitOperationsAction.Move:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for move.");
                if (request.TargetArchiverId == null)
                    throw new ArgumentException("Parameter 'target_archiver_id' is required for move.");
                await client.RequireEntityType(request.Id.Value, "id", "VideoUnit");
                await client.RequireEntityType(request.TargetArchiverId.Value, "target_archiver_id", "Role");
                return await client.ExecuteAction($"MoveVideoUnit({request.Id},{request.TargetArchiverId})");

            case VideoUnitOperationsAction.Reconnect:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for reconnect.");
                await client.RequireEntityType(request.Id.Value, "id", "VideoUnit");
                return await client.ExecuteAction($"ReconnectVideoUnit({request.Id})");

            case VideoUnitOperationsAction.Reboot:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for reboot.");
                await client.RequireEntityType(request.Id.Value, "id", "VideoUnit");
                return await client.ExecuteAction($"RebootVideoUnit({request.Id})");

            case VideoUnitOperationsAction.DeleteGhostCamera:
                if (request.CameraId == null)
                    throw new ArgumentException("Parameter 'camera_id' is required for delete_ghost_camera.");
                return await client.ExecuteAction($"DeleteGhostCamera({request.CameraId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
