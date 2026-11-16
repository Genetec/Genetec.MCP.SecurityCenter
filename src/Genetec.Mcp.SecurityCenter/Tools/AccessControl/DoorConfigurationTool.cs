using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

public enum DoorAccessRuleSide { Default, Both, In, Out }

public enum DoorSidePath
{
    DoorSideIn,
    DoorSideOut
}

public enum DoorSideMethod
{
    AddCardholder,
    RemoveCardholder,
    AddCardholderGroup,
    RemoveCardholderGroup,
    SetDevice
}

public enum AccessPointType
{
    None,
    CardReader,
    Rex,
    DoorSensor,
    AreaDirection,
    PullStation,
    DoorLock,
    Zone,
    Floor,
    Buzzer,
    FloorInput,
    FloorOutput,
    EntrySensor,
    PlateReader,
    IntrusionAreaArming,
    IntrusionAreaDirection,
    LockSensor,
    OperatorConfirmation
}

public enum DoorSideComponent
{
    Reader,
    Rex,
    EntrySensor
}

public enum DoorConfigAction
{
    Unlock,
    OpenDoor,
    SetBuzzer,
    OverrideLockSchedule,
    SetMaintenanceMode,
    IsOpen,
    IsLocked,
    GetAccessPoint,
    SetAccessPoint,
    GetDoorSides,
    GetDoorSide,
    SetDoorSide,
    AddDoorSideAccessRule,
    RemoveDoorSideAccessRule,
    AddAccessPointCardholder,
    RemoveAccessPointCardholder,
    AddAccessPointCardholderGroup,
    RemoveAccessPointCardholderGroup,
    SetAccessPointDevice,
    SetUnit,
    SetInterfaceModule,
    ActivityReport
}

public sealed record DoorConfigRequest(
    [property: Description("Action to perform"), Required] DoorConfigAction Action,
    [property: Description("Door GUID"), Required] Guid Id,
    [property: Description("Door side GUID")] Guid? DoorSideId = null,
    [property: Description("Access point GUID")] Guid? AccessPointId = null,
    [property: Description("Access rule GUID — for add_door_side_access_rule / remove_door_side_access_rule")] Guid? AccessRuleId = null,
    [property: Description("Door side for access rule — for add_door_side_access_rule / remove_door_side_access_rule")] DoorAccessRuleSide? AccessRuleSide = null,
    [property: Description("Typed properties for set_access_point / set_door_side")] DoorEntityUpdateRequest? Update = null,
    [property: Description("Whether the buzzer is active (true/false) — for set_buzzer")] bool? BuzzerState = null,
    [property: Description("Enable or disable maintenance mode — for set_maintenance_mode")] bool? EnableMaintenance = null,
    [property: Description("Unlock state for override_lock_schedule")]
    bool? Unlock = null,
    [property: Description("Override start time ISO 8601 — for override_lock_schedule"), Iso8601]
    string? StartTime = null,
    [property: Description("Override end time ISO 8601 — for override_lock_schedule"), Iso8601]
    string? EndTime = null,
    [property: Description("Cardholder GUID — for add_access_point_cardholder / remove_access_point_cardholder")] Guid? CardholderId = null,
    [property: Description("Cardholder group GUID — for add_access_point_cardholder_group / remove_access_point_cardholder_group")] Guid? CardholderGroupId = null,
    [property: Description("Device GUID — for set_access_point_device")] Guid? DeviceId = null,
    [property: Description("Unit GUID — for set_unit")] Guid? UnitId = null,
    [property: Description("Interface module GUID — for set_interface_module")] Guid? InterfaceId = null,
    [property: Description("Activity report start time (ISO 8601); provide with time_end"), Iso8601] string? TimeStart = null,
    [property: Description("Activity report end time (ISO 8601); provide with time_start"), Iso8601] string? TimeEnd = null);

[McpServerToolType]
public class DoorConfigurationTool
{
    [McpServerTool(
        Name = "door_configuration",
        Title = "Door Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Configure door access points, door sides, rules, and control operations. " +
        "Use when managing door hardware setup, assigning access rules to door sides, " +
        "controlling locks, checking door state, or managing maintenance mode. " +
        "Use unlock for a momentary unlock pulse. Use open_door for the normal door-open command. " +
        "Use override_lock_schedule when the door must remain unlocked (or locked) for a specified " +
        "start/end interval; provide unlock, start_time, and end_time for that action. " +
        "For activity_report, optionally provide time_start and time_end together to limit events. " +
        "Actions: unlock, open_door, set_buzzer, override_lock_schedule, set_maintenance_mode, " +
        "is_open, is_locked, get_access_point, set_access_point, " +
        "get_door_sides, get_door_side, set_door_side, add_door_side_access_rule, remove_door_side_access_rule, " +
        "add_access_point_cardholder, remove_access_point_cardholder, " +
        "add_access_point_cardholder_group, remove_access_point_cardholder_group, " +
        "set_access_point_device, set_unit, set_interface_module, activity_report")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Door configuration request")] DoorConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case DoorConfigAction.Unlock:
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.ExecuteAction($"UnlockDoor({request.Id})");

            case DoorConfigAction.OpenDoor:
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.ExecuteAction($"Open({request.Id})");

            case DoorConfigAction.SetBuzzer:
                if (request.BuzzerState is null)
                    throw new ArgumentException("Parameter 'buzzer_state' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.ExecuteAction($"SetBuzzerState({request.Id},{request.BuzzerState.Value.ToString().ToLowerInvariant()})");

            case DoorConfigAction.OverrideLockSchedule:
                if (request.Unlock is null)
                    throw new ArgumentException("Parameter 'unlock' is required for this action.");
                if (request.StartTime is null)
                    throw new ArgumentException("Parameter 'start_time' is required for this action.");
                if (request.EndTime is null)
                    throw new ArgumentException("Parameter 'end_time' is required for this action.");

                var startTime = ParseOverrideTime(request.StartTime, "start_time");
                var endTime = ParseOverrideTime(request.EndTime, "end_time");
                if (endTime <= startTime)
                    throw new ArgumentException("Parameter 'end_time' must be later than 'start_time'.");

                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.ExecuteAction(
                    $"OverrideLockingScheduleOnDoor({request.Id},{request.Unlock.Value.ToString().ToLowerInvariant()},{FormatWebSdkTime(startTime)},{FormatWebSdkTime(endTime)})");

            case DoorConfigAction.SetMaintenanceMode:
                if (request.EnableMaintenance is null)
                    throw new ArgumentException("Parameter 'enable_maintenance' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.ExecuteEntityMethod(
                    request.Id.ToString(),
                    "SetMaintenanceMode",
                    request.EnableMaintenance.Value.ToString().ToLowerInvariant());

            case DoorConfigAction.IsOpen:
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.GetEntityField(request.Id.ToString(), "IsOpen");

            case DoorConfigAction.IsLocked:
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.GetEntityField(request.Id.ToString(), "IsLocked");

            case DoorConfigAction.GetAccessPoint:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for action 'get_access_point'.");
                return await client.GetDoorAccessPoint(request.Id, request.AccessPointId.Value, "access_point_id");

            case DoorConfigAction.SetAccessPoint:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for action 'set_access_point'.");
                if (request.Update is null)
                    throw new ArgumentException("Parameter 'update' is required for action 'set_access_point'.");
                await client.GetDoorAccessPoint(request.Id, request.AccessPointId.Value, "access_point_id");
                return await client.UpdateEntity(request.AccessPointId.Value.ToString(), request.Update);

            case DoorConfigAction.GetDoorSides:
                await client.RequireEntityType(request.Id, "id", "Door");
                return await client.GetEntityField(request.Id.ToString(), "DoorSides");

            case DoorConfigAction.GetDoorSide:
                if (request.DoorSideId is null)
                    throw new ArgumentException("Parameter 'door_side_id' is required for action 'get_door_side'.");
                return await client.GetDoorAccessPoint(request.Id, request.DoorSideId.Value, "door_side_id", requireDoorSide: true);

            case DoorConfigAction.SetDoorSide:
                if (request.DoorSideId is null)
                    throw new ArgumentException("Parameter 'door_side_id' is required for action 'set_door_side'.");
                if (request.Update is null)
                    throw new ArgumentException("Parameter 'update' is required for action 'set_door_side'.");
                await client.GetDoorAccessPoint(request.Id, request.DoorSideId.Value, "door_side_id", requireDoorSide: true);
                return await client.UpdateEntity(request.DoorSideId.Value.ToString(), request.Update);

            case DoorConfigAction.AddDoorSideAccessRule:
                if (request.AccessRuleId is null)
                    throw new ArgumentException("Parameter 'access_rule_id' is required for this action.");
                if (request.AccessRuleSide is null)
                    throw new ArgumentException("Parameter 'access_rule_side' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                await client.RequireEntityType(request.AccessRuleId.Value, "access_rule_id", "AccessRule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddAccessRule", $"{request.AccessRuleId}\\,{request.AccessRuleSide.Value}");

            case DoorConfigAction.RemoveDoorSideAccessRule:
                if (request.AccessRuleId is null)
                    throw new ArgumentException("Parameter 'access_rule_id' is required for this action.");
                if (request.AccessRuleSide is null)
                    throw new ArgumentException("Parameter 'access_rule_side' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                await client.RequireEntityType(request.AccessRuleId.Value, "access_rule_id", "AccessRule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveAccessRule", $"{request.AccessRuleId}\\,{request.AccessRuleSide.Value}");

            case DoorConfigAction.AddAccessPointCardholder:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.CardholderId is null)
                    throw new ArgumentException("Parameter 'cardholder_id' is required for this action.");
                return await client.ExecuteDoorSideMethod(
                    request.Id, request.AccessPointId.Value, DoorSideMethod.AddCardholder, request.CardholderId.Value);

            case DoorConfigAction.RemoveAccessPointCardholder:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.CardholderId is null)
                    throw new ArgumentException("Parameter 'cardholder_id' is required for this action.");
                return await client.ExecuteDoorSideMethod(
                    request.Id, request.AccessPointId.Value, DoorSideMethod.RemoveCardholder, request.CardholderId.Value);

            case DoorConfigAction.AddAccessPointCardholderGroup:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.CardholderGroupId is null)
                    throw new ArgumentException("Parameter 'cardholder_group_id' is required for this action.");
                return await client.ExecuteDoorSideMethod(
                    request.Id, request.AccessPointId.Value, DoorSideMethod.AddCardholderGroup, request.CardholderGroupId.Value);

            case DoorConfigAction.RemoveAccessPointCardholderGroup:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.CardholderGroupId is null)
                    throw new ArgumentException("Parameter 'cardholder_group_id' is required for this action.");
                return await client.ExecuteDoorSideMethod(
                    request.Id, request.AccessPointId.Value, DoorSideMethod.RemoveCardholderGroup, request.CardholderGroupId.Value);

            case DoorConfigAction.SetAccessPointDevice:
                if (request.AccessPointId is null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.DeviceId is null)
                    throw new ArgumentException("Parameter 'device_id' is required for this action.");
                return await client.ExecuteDoorSideMethod(
                    request.Id, request.AccessPointId.Value, DoorSideMethod.SetDevice, request.DeviceId.Value);

            case DoorConfigAction.SetUnit:
                if (request.UnitId is null)
                    throw new ArgumentException("Parameter 'unit_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                await client.RequireEntityType(request.UnitId.Value, "unit_id", "Unit");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetPreferredUnit", request.UnitId!.ToString());

            case DoorConfigAction.SetInterfaceModule:
                if (request.InterfaceId is null)
                    throw new ArgumentException("Parameter 'interface_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Door");
                await client.RequireEntityType(request.InterfaceId.Value, "interface_id", "InterfaceModule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "SetPreferredInterface", request.InterfaceId!.ToString());

            case DoorConfigAction.ActivityReport:
                return await client.RunReport("DoorActivity",
                    ActivityReportQuery.WithTimeRange($"Doors@{request.Id}", request.TimeStart, request.TimeEnd));

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static DateTimeOffset ParseOverrideTime(string value, string parameterName)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            throw new ArgumentException($"Parameter '{parameterName}' must be a valid ISO 8601 timestamp.");

        return parsed;
    }

    private static string FormatWebSdkTime(DateTimeOffset value)
        => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}

public sealed record DoorEntityUpdateRequest(
    [Description("New entity name")] string? Name = null,
    [Description("Entity description")] string? Description = null,
    [Description("Whether the entity is hidden from the user interface")] bool? HiddenFromUI = null,
    [Description("Logical identifier")] int? LogicalId = null);

public sealed record AccessPointSideResponse(
    [property: JsonPropertyName("AccessPointSide")] DoorSidePath AccessPointSide,
    AccessPointType AccessPointType
);
