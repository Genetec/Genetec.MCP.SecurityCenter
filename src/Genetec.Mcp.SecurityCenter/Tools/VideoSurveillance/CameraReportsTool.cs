using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraReportsTool
{
    [McpServerTool(
        Name = "camera_reports",
        Title = "Camera Reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Run camera-related reports in Security Center. " +
        "Actions: event_report, video_sequence_report, get_video_files, get_metadata_streams.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera reports request")] CameraReportsRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CameraReportAction.EventReport:
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for event_report.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for event_report.");
                return await client.RunReport("CameraEvent",
                    $"Cameras@{request.Id},TimeRange.SetTimeRange({Esc(request.TimeStart)},{Esc(request.TimeEnd)})");

            case CameraReportAction.VideoSequenceReport:
                return await client.RunReport("VideoSequence", $"Cameras@{request.Id}");

            case CameraReportAction.GetVideoFiles:
                return await client.RunReport("VideoFile", $"Cameras@{request.Id}");

            case CameraReportAction.GetMetadataStreams:
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.GetEntityField(request.Id.ToString(), "MetadataStreams");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CameraReportAction
{
    EventReport,
    VideoSequenceReport,
    GetVideoFiles,
    GetMetadataStreams
}

public sealed record CameraReportsRequest(
    [property: Description("Camera GUID"), Required] Guid Id,
    [property: Description("Action to perform"), Required] CameraReportAction Action,
    [property: Description("Report start time ISO 8601 (for event_report)"), Iso8601] string? TimeStart = null,
    [property: Description("Report end time ISO 8601 (for event_report)"), Iso8601] string? TimeEnd = null);
