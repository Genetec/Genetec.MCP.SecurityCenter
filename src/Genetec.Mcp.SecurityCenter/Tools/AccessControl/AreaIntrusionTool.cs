using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum AreaIntrusionAction
{
    PostponeIntrusionArming,
    TriggerIntrusionAlarm,
    SilenceIntrusionAlarm,
    AcknowledgeIntrusionAlarm,
    BypassInputDevice,
    ClearInputDeviceBypass
}

public sealed record AreaIntrusionRequest(
    [property: Description("Action to perform"), Required] AreaIntrusionAction Action,
    [property: Description("Intrusion area GUID")] Guid? IntrusionAreaId = null,
    [property: Description("Input device GUID")] Guid? DeviceId = null,
    [property: Description("Postpone delay in seconds (required for postpone_intrusion_arming)"), Range(0, int.MaxValue)] int? Delay = null,
    [property: Description("Arming countdown in seconds after postponement expires (required for postpone_intrusion_arming; at least 1 second)"), Range(1, int.MaxValue)] int? ArmingDelay = null);

[McpServerToolType]
public class AreaIntrusionTool
{
    [McpServerTool(
        Name = "area_intrusion",
        Title = "Area Intrusion Control",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Control intrusion detection alarms and input device bypass. " +
        "Actions: postpone_intrusion_arming, " +
        "trigger_intrusion_alarm, silence_intrusion_alarm, acknowledge_intrusion_alarm, " +
        "bypass_input_device, clear_input_device_bypass")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area intrusion control request")] AreaIntrusionRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case AreaIntrusionAction.PostponeIntrusionArming:
                if (request.IntrusionAreaId is null)
                    throw new ArgumentException("parameter 'intrusion_area_id' is required for this action.");
                if (request.Delay is null)
                    throw new ArgumentException("Parameter 'delay' is required for action 'postpone_intrusion_arming'.", "delay");
                if (request.ArmingDelay is null)
                    throw new ArgumentException("Parameter 'arming_delay' is required for action 'postpone_intrusion_arming'.", "arming_delay");
                await client.RequireEntityType(request.IntrusionAreaId.Value, "intrusion_area_id", "IntrusionArea");
                return await client.ExecuteEntityMethod(request.IntrusionAreaId.Value.ToString(), "PostponeArming",
                    $"{TimeSpan.FromSeconds(request.Delay.Value).ToString("c", CultureInfo.InvariantCulture)},{TimeSpan.FromSeconds(request.ArmingDelay.Value).ToString("c", CultureInfo.InvariantCulture)}");

            case AreaIntrusionAction.TriggerIntrusionAlarm:
                if (request.IntrusionAreaId is null)
                    throw new ArgumentException("parameter 'intrusion_area_id' is required for this action.");
                await client.RequireEntityType(request.IntrusionAreaId.Value, "intrusion_area_id", "IntrusionArea");
                return await client.ExecuteAction($"TriggerIntrusionAreaAlarm({request.IntrusionAreaId})");

            case AreaIntrusionAction.SilenceIntrusionAlarm:
                if (request.IntrusionAreaId is null)
                    throw new ArgumentException("parameter 'intrusion_area_id' is required for this action.");
                await client.RequireEntityType(request.IntrusionAreaId.Value, "intrusion_area_id", "IntrusionArea");
                return await client.ExecuteAction($"SilenceIntrusionAreaAlarm({request.IntrusionAreaId})");

            case AreaIntrusionAction.AcknowledgeIntrusionAlarm:
                if (request.IntrusionAreaId is null)
                    throw new ArgumentException("parameter 'intrusion_area_id' is required for this action.");
                await client.RequireEntityType(request.IntrusionAreaId.Value, "intrusion_area_id", "IntrusionArea");
                return await client.ExecuteAction($"AcknowledgeIntrusionAreaAlarm({request.IntrusionAreaId})");

            case AreaIntrusionAction.BypassInputDevice:
                if (request.DeviceId is null)
                    throw new ArgumentException("parameter 'device_id' is required for this action.");
                await client.RequireEntityType(request.DeviceId.Value, "device_id", "Device");
                return await client.ExecuteAction($"BypassIntrusionInputDevice({request.DeviceId})");

            case AreaIntrusionAction.ClearInputDeviceBypass:
                if (request.DeviceId is null)
                    throw new ArgumentException("parameter 'device_id' is required for this action.");
                await client.RequireEntityType(request.DeviceId.Value, "device_id", "Device");
                return await client.ExecuteAction($"ClearIntrusionInputDeviceBypass({request.DeviceId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}
