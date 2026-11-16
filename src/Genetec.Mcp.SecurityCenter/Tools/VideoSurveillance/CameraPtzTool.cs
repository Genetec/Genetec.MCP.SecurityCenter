using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraPtzTool
{
    [McpServerTool(
        Name = "camera_ptz",
        Title = "Camera PTZ Control",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Control PTZ (Pan-Tilt-Zoom) cameras in Security Center. " +
        "Use when moving a camera, navigating to a preset, running a patrol pattern, " +
        "starting/stopping a tour, or locking PTZ control. " +
        "Actions: pan_tilt, zoom, focus, iris, go_to_preset, save_preset, " +
        "run_pattern, start_pattern, stop_pattern, start_tour, stop_tour, " +
        "lock, unlock, menu_on, menu_off, menu_enter, menu_left, menu_right, menu_up, menu_down. " +
        "Zoom, focus, and iris require an operation and speed. Pattern commands require a pattern ID; " +
        "start_tour requires a tour ID.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("PTZ request")] CameraPtzRequest request,
        CancellationToken cancellationToken = default)
    {
        var actionQuery = BuildActionQuery(request);
        var client = sessionManager.GetClient(cancellationToken);
        await client.RequireEntityType(request.Id, "id", "Camera");
        return await client.ExecuteAction(actionQuery);
    }

    internal static string BuildActionQuery(CameraPtzRequest request)
    {
        switch (request.Command)
        {
            case PtzCommand.PanTilt:
                if (request.PanSpeed is null) throw new ArgumentException("Parameter 'pan_speed' is required for command 'pan_tilt'.");
                if (request.TiltSpeed is null) throw new ArgumentException("Parameter 'tilt_speed' is required for command 'pan_tilt'.");
                return $"SendPtzCommand({request.Id},StartPanTilt,{request.PanSpeed.Value},{request.TiltSpeed.Value})";

            case PtzCommand.Zoom:
                if (request.Speed is null) throw new ArgumentException("Parameter 'speed' is required for command 'zoom'.");
                if (request.ZoomOperation is null) throw new ArgumentException("Parameter 'zoom_operation' is required for command 'zoom'.");
                return $"SendPtzCommand({request.Id},StartZoom,{(int)request.ZoomOperation.Value},{request.Speed.Value})";

            case PtzCommand.Focus:
                if (request.Speed is null) throw new ArgumentException("Parameter 'speed' is required for command 'focus'.");
                if (request.FocusOperation is null) throw new ArgumentException("Parameter 'focus_operation' is required for command 'focus'.");
                return $"SendPtzCommand({request.Id},StartFocus,{(int)request.FocusOperation.Value},{request.Speed.Value})";

            case PtzCommand.Iris:
                if (request.Speed is null) throw new ArgumentException("Parameter 'speed' is required for command 'iris'.");
                if (request.IrisOperation is null) throw new ArgumentException("Parameter 'iris_operation' is required for command 'iris'.");
                return $"SendPtzCommand({request.Id},StartIris,{(int)request.IrisOperation.Value},{request.Speed.Value})";

            case PtzCommand.GoToPreset:
                if (request.PresetNumber is null) throw new ArgumentException("Parameter 'preset_number' is required for command 'go_to_preset'.");
                return $"SendPtzCommand({request.Id},GoToPreset,{request.PresetNumber.Value},0)";

            case PtzCommand.SavePreset:
                if (request.PresetNumber is null) throw new ArgumentException("Parameter 'preset_number' is required for command 'save_preset'.");
                return $"SendPtzCommand({request.Id},SetPreset,{request.PresetNumber.Value},0)";

            case PtzCommand.RunPattern:
                if (request.PatternId is null) throw new ArgumentException("Parameter 'pattern_id' is required for command 'run_pattern'.");
                return $"SendPtzCommand({request.Id},RunPattern,{request.PatternId.Value},0)";

            case PtzCommand.StartPattern:
                if (request.PatternId is null) throw new ArgumentException("Parameter 'pattern_id' is required for command 'start_pattern'.");
                return $"SendPtzCommand({request.Id},StartPattern,{request.PatternId.Value},0)";

            case PtzCommand.StopPattern:
                if (request.PatternId is null) throw new ArgumentException("Parameter 'pattern_id' is required for command 'stop_pattern'.");
                return $"SendPtzCommand({request.Id},StopPattern,{request.PatternId.Value},0)";

            case PtzCommand.StartTour:
                if (request.TourId is null) throw new ArgumentException("Parameter 'tour_id' is required for command 'start_tour'.");
                return $"SendPtzCommand({request.Id},StartPresetTour,{request.TourId.Value},0)";

            case PtzCommand.StopTour:
                return $"SendPtzCommand({request.Id},StopPresetTour,0,0)";

            case PtzCommand.Lock:
                return $"SendPtzCommand({request.Id},Lock,0,0)";

            case PtzCommand.Unlock:
                return $"SendPtzCommand({request.Id},OverrideLock,0,0)";

            case PtzCommand.MenuOn:
                return $"SendPtzCommand({request.Id},MenuOn,0,0)";

            case PtzCommand.MenuOff:
                return $"SendPtzCommand({request.Id},MenuOff,0,0)";

            case PtzCommand.MenuEnter:
                return $"SendPtzCommand({request.Id},Enter,0,0)";

            case PtzCommand.MenuLeft:
                return $"SendPtzCommand({request.Id},MenuLeft,0,0)";

            case PtzCommand.MenuRight:
                return $"SendPtzCommand({request.Id},MenuRight,0,0)";

            case PtzCommand.MenuUp:
                return $"SendPtzCommand({request.Id},MenuUp,0,0)";

            case PtzCommand.MenuDown:
                return $"SendPtzCommand({request.Id},MenuDown,0,0)";

            default:
                throw new ArgumentException($"Unknown PTZ command: {request.Command}");
        }
    }

}

public enum PtzCommand
{
    PanTilt, Zoom, Focus, Iris,
    GoToPreset, SavePreset,
    RunPattern, StartPattern, StopPattern,
    StartTour, StopTour,
    Lock, Unlock,
    MenuOn, MenuOff, MenuEnter, MenuLeft, MenuRight, MenuUp, MenuDown
}

public enum PtzZoomOperation
{
    ZoomIn = 0,
    ZoomOut = 1
}

public enum PtzFocusOperation
{
    Near = 0,
    Far = 1
}

public enum PtzIrisOperation
{
    Open = 0,
    Close = 1
}

public sealed record CameraPtzRequest(
    [property: Description("Camera GUID"), Required] Guid Id,
    [property: Description("PTZ command to execute"), Required] PtzCommand Command,
    [Description("Pan speed (for pan_tilt)")] int? PanSpeed = null,
    [Description("Tilt speed (for pan_tilt)")] int? TiltSpeed = null,
    [Description("Speed value (for zoom, focus, iris)")] int? Speed = null,
    [Description("Preset number (for go_to_preset, save_preset)")] int? PresetNumber = null,
    [Description("Pattern number (for run_pattern, start_pattern, stop_pattern)")] int? PatternId = null,
    [Description("Tour number (for start_tour)")] int? TourId = null,
    [Description("Zoom operation: zoom_in or zoom_out")] PtzZoomOperation? ZoomOperation = null,
    [Description("Focus operation: near or far")] PtzFocusOperation? FocusOperation = null,
    [Description("Iris operation: open or close")] PtzIrisOperation? IrisOperation = null);
