using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraThumbnailsTool
{
    [McpServerTool(
        Name = "camera_thumbnails",
        Title = "Get Camera Thumbnails",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve thumbnail images from recorded camera video. " +
        "Returns base64-encoded image data for one or more cameras within a time range.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera thumbnails request")] CameraThumbnailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetThumbnails(request.CameraIds, request.TimeStart, request.TimeEnd, request.Width, request.Count);
    }
}

public sealed record CameraThumbnailsRequest(
    [property: Description("1 to 10 camera GUIDs; at most 50 images total across cameras"), Required, MinLength(1), MaxLength(10)] List<Guid> CameraIds,
    [property: Description("Time range start ISO 8601"), Required, MinLength(1), Iso8601] string TimeStart,
    [property: Description("Time range end ISO 8601"), Required, MinLength(1), Iso8601] string TimeEnd,
    [property: Description("Requested thumbnail width in pixels; positive values only"), Range(1, int.MaxValue)] int Width = 640,
    [property: Description("Requested thumbnails per camera; positive values only and at most 50 images total across cameras"), Range(1, int.MaxValue)] int Count = 1);
