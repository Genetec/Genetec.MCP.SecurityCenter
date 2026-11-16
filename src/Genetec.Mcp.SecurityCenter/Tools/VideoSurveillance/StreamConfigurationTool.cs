using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

public enum StreamIntervalMeasure
{
    Images,
    Seconds
}

public enum StreamRecordingFrameInterval
{
    RecordAllFrames,
    RecordAllKeyFrames,
    Record15FramePer1Second,
    Record10FramePer1Second,
    Record6FramePer1Second,
    Record5FramePer1Second,
    Record2FramePer1Second,
    Record1FramePer1Second,
    Record1FramePer2Seconds,
    Record1FramePer5Seconds,
    Record1FramePer10Seconds,
    Record1FramePer15Seconds,
    Record1FramePer20Seconds,
    Record1FramePer30Seconds,
    Record1FramePer1Minute,
    Record1FramePer2Minutes,
    Record1FramePer5Minutes
}

public enum StreamConfigAction
{
    SetBitRate,
    SetConstantBitRate,
    SetFrameRate,
    SetDynamicFrameRate,
    SetDynamicFrameRateMin,
    SetResolution,
    SetKeyFrameInterval,
    SetRecordingFrameInterval
}

public sealed record StreamConfigRequest(
    [property: Description("Stream GUID (get from stream_management list_by_camera)"), Required] Guid Id,
    [property: Description("Action to perform"), Required] StreamConfigAction Action,
    [Description("Schedule GUID; defaults to the Always schedule")] Guid? ScheduleId = null,
    [Description("Target bitrate in kbps (for set_bit_rate)")] int? BitRate = null,
    [Description("Whether constant bitrate encoding is enabled (for set_constant_bit_rate)")] bool? ConstantBitRate = null,
    [Description("Frames per second (for set_frame_rate, set_dynamic_frame_rate_min)")] double? FrameRate = null,
    [Description("Whether dynamic frame rate is enabled (for set_dynamic_frame_rate)")] bool? DynamicFrameRate = null,
    [Description("Horizontal resolution in pixels (for set_resolution)")] int? Width = null,
    [Description("Vertical resolution in pixels (for set_resolution)")] int? Height = null,
    [Description("Key frame interval value (for set_key_frame_interval)")] int? Interval = null,
    [Description("Interval measure (for set_key_frame_interval)")] StreamIntervalMeasure? IntervalMeasure = null,
    [Description("Recording frame interval (for set_recording_frame_interval)")] StreamRecordingFrameInterval? RecordingFrameInterval = null);

[McpServerToolType]
public class StreamConfigurationTool
{
    [McpServerTool(
        Name = "stream_configuration",
        Title = "Video Stream Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Configure video stream encoding parameters including bitrate, frame rate, " +
        "resolution, and key frame interval. " +
        "Use when tuning camera stream quality, bandwidth, or storage settings. " +
        "First use stream_management list_by_camera to get stream GUIDs. " +
        "Actions: set_bit_rate, set_constant_bit_rate, set_frame_rate, set_dynamic_frame_rate, " +
        "set_dynamic_frame_rate_min, set_resolution, set_key_frame_interval, " +
        "set_recording_frame_interval.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Stream configuration request")] StreamConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        var scheduleId = request.ScheduleId ?? new Guid("00000000-0000-0000-0000-000000000006");
        var schedule = scheduleId.ToString();

        switch (request.Action)
        {
            case StreamConfigAction.SetBitRate:
                if (request.BitRate is null)
                    throw new ArgumentException("Parameter 'bit_rate' is required for set_bit_rate.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetBitRate", $"{schedule},{request.BitRate.Value.ToString(CultureInfo.InvariantCulture)}");

            case StreamConfigAction.SetConstantBitRate:
                if (request.ConstantBitRate is null)
                    throw new ArgumentException("Parameter 'constant_bit_rate' is required for set_constant_bit_rate.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetConstantBitRate", $"{schedule},{request.ConstantBitRate.Value.ToString().ToLowerInvariant()}");

            case StreamConfigAction.SetFrameRate:
                if (request.FrameRate is null)
                    throw new ArgumentException("Parameter 'frame_rate' is required for set_frame_rate.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetFrameRate", $"{schedule},{request.FrameRate.Value.ToString(CultureInfo.InvariantCulture)}");

            case StreamConfigAction.SetDynamicFrameRate:
                if (request.DynamicFrameRate is null)
                    throw new ArgumentException("Parameter 'dynamic_frame_rate' is required for set_dynamic_frame_rate.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetDynamicFrameRate", $"{schedule},{request.DynamicFrameRate.Value.ToString().ToLowerInvariant()}");

            case StreamConfigAction.SetDynamicFrameRateMin:
                if (request.FrameRate is null)
                    throw new ArgumentException("Parameter 'frame_rate' is required for set_dynamic_frame_rate_min.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetDynamicFrameRateMinimumFrameRate", $"{schedule},{request.FrameRate.Value.ToString(CultureInfo.InvariantCulture)}");

            case StreamConfigAction.SetResolution:
                if (request.Width is null)
                    throw new ArgumentException("Parameter 'width' is required for set_resolution.");
                if (request.Height is null)
                    throw new ArgumentException("Parameter 'height' is required for set_resolution.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetResolution", $"{schedule},StreamSupportedResolution(){{Height={request.Height.Value},Width={request.Width.Value}}}");

            case StreamConfigAction.SetKeyFrameInterval:
                if (request.Interval is null)
                    throw new ArgumentException("Parameter 'interval' is required for set_key_frame_interval.");
                if (request.IntervalMeasure is null)
                    throw new ArgumentException("Parameter 'interval_measure' is required for set_key_frame_interval.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetKeyFrameInterval", $"{schedule},{request.Interval.Value.ToString(CultureInfo.InvariantCulture)},{request.IntervalMeasure.Value}");

            case StreamConfigAction.SetRecordingFrameInterval:
                if (request.RecordingFrameInterval is null)
                    throw new ArgumentException("Parameter 'recording_frame_interval' is required for set_recording_frame_interval.");
                await client.RequireEntityType(request.Id, "id", "Stream");
                await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetRecordingFrameInterval", $"{schedule},{request.RecordingFrameInterval.Value}");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
