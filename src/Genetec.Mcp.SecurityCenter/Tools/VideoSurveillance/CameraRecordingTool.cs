using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraRecordingTool
{
    [McpServerTool(
        Name = "camera_recording",
        Title = "Camera Recording Control",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
     OpenWorld = true),
     Description(
        "Control camera recording and maintenance state in Security Center. " +
        "Use enable_maintenance to manually place a camera in maintenance mode; Security Center " +
        "automatically disables it after the supplied end_time. An optional reason can be provided. " +
        "Actions: start_recording, stop_recording, get_streams, enable_maintenance.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera recording request")] CameraRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CameraRecordingAction.StartRecording:
                if (request.RecordingLength is null)
                    throw new ArgumentException("Parameter 'recording_length' is required for action 'start_recording'.");
                if (request.Locked is null)
                    throw new ArgumentException("Parameter 'locked' is required for action 'start_recording'.");
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.ExecuteEntityMethod(
                    request.Id.ToString(),
                    "StartRecording",
                    $"{TimeSpan.FromSeconds(request.RecordingLength.Value).ToString("c", CultureInfo.InvariantCulture)},{request.Locked.Value.ToString().ToLowerInvariant()}");

            case CameraRecordingAction.StopRecording:
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "StopRecording");

            case CameraRecordingAction.GetStreams:
                await client.RequireEntityType(request.Id, "id", "Camera");
                return await client.GetEntityField(request.Id.ToString(), "Streams");

            case CameraRecordingAction.EnableMaintenance:
                if (request.EndTime is null) throw new ArgumentException("Parameter 'end_time' is required for action 'enable_maintenance'.");
                await client.RequireEntityType(request.Id, "id", "Camera");
                var endTime = request.EndTime.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return await client.ExecuteEntityMethod(
                    request.Id.ToString(),
                    "EnableMaintenance",
                    $"{endTime},{InputValidation.Esc(request.Reason)}");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CameraRecordingAction
{
    StartRecording,
    StopRecording,
    GetStreams,
    EnableMaintenance
}

public sealed record CameraRecordingRequest(
    [property: Description("Camera GUID"), Required] Guid Id,
    [property: Description("Action to perform. enable_maintenance manually starts maintenance and automatically ends it at end_time."), Required] CameraRecordingAction Action,
    [property: Description("Recording duration in seconds (for start_recording)"), Range(1, int.MaxValue)] int? RecordingLength = null,
    [Description("Whether the recording is protected from deletion (for start_recording)")] bool? Locked = null,
    [property: Description("UTC time when Security Center automatically disables manually enabled maintenance; required for enable_maintenance")] DateTime? EndTime = null,
    [Description("Optional reason for manually enabling maintenance (for enable_maintenance)")] string? Reason = null);
