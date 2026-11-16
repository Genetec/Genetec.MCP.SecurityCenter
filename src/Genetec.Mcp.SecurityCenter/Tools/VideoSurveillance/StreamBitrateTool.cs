using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class StreamBitrateTool
{
    private static readonly Guid AlwaysSchedule = new("00000000-0000-0000-0000-000000000006");

    [McpServerTool(
        Name = "stream_bitrate",
        Title = "Video Stream Bitrate",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Set a video stream bitrate for a recording schedule through the Web SDK.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Video stream bitrate request")] StreamBitrateRequest request,
        CancellationToken cancellationToken = default)
    {
        var scheduleId = request.ScheduleId ?? AlwaysSchedule;
        var client = sessionManager.GetClient(cancellationToken);
        await client.RequireEntityType(request.Id, "id", "Stream");
        await client.RequireEntityType(scheduleId, "schedule_id", "Schedule");
        return await client.ExecuteEntityMethod(
            request.Id.ToString(),
            "SetBitRate",
            $"{scheduleId},{request.BitRate}");
    }
}

public sealed record StreamBitrateRequest(
    [property: Description("Video stream GUID"), Required] Guid Id,
    [property: Description("Target bitrate in kbps"), Required, Range(1, int.MaxValue)] int BitRate,
    [property: Description("Schedule GUID; defaults to the Always schedule")] Guid? ScheduleId = null);
