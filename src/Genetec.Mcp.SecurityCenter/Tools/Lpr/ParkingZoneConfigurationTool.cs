using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Lpr;

[McpServerToolType]
public class ParkingZoneConfigurationTool
{
    [McpServerTool(
        Name = "parking_zone_configuration",
        Title = "Parking Zone Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Configure parking zone cameras, rules, and capacity. " +
                 "Actions: get_cameras (requires camera_id); " +
                 "add_lpr_camera (requires camera_id, position, and relative_motion); " +
                 "remove_lpr_camera (requires camera_id as the LPR access point GUID); " +
                 "add_rule, remove_rule (require rule_id); get_capacity. " +
                 "The Web SDK parking-zone properties are LprCamerasInfo and ParkingZoneRules.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Parking zone configuration request")] ParkingZoneConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        switch (request.Action)
        {
            case ParkingZoneConfigurationAction.GetCameras:
                if (request.CameraId is null) throw new ArgumentException("Parameter 'camera_id' is required for this action.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "Device");
                return await client.ExecuteEntityMethod(
                    request.ZoneId.ToString(),
                    "GetLprCameraInformation",
                    request.CameraId.ToString());

            case ParkingZoneConfigurationAction.AddLprCamera:
                if (request.CameraId is null) throw new ArgumentException("Parameter 'camera_id' is required for this action.");
                if (request.Position is null) throw new ArgumentException("Parameter 'position' is required for action 'add_lpr_camera'.");
                if (request.RelativeMotion is null) throw new ArgumentException("Parameter 'relative_motion' is required for action 'add_lpr_camera'.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "Device");
                return await client.ExecuteEntityMethod(
                    request.ZoneId.ToString(),
                    "AddLprCamera",
                    $"{request.CameraId},{request.Position},{request.RelativeMotion}");

            case ParkingZoneConfigurationAction.RemoveLprCamera:
                if (request.CameraId is null) throw new ArgumentException("Parameter 'camera_id' is required for this action.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "AccessPoint");
                return await client.ExecuteEntityMethod(
                    request.ZoneId.ToString(),
                    "RemoveLprCamera",
                    request.CameraId.ToString());

            case ParkingZoneConfigurationAction.AddRule:
                if (request.RuleId is null) throw new ArgumentException("Parameter 'rule_id' is required for this action.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                await client.RequireEntityType(request.RuleId.Value, "rule_id", "ParkingRule");
                return await client.ExecuteEntityMethod(
                    request.ZoneId.ToString(),
                    "AddParkingZoneRule",
                    request.RuleId.ToString());

            case ParkingZoneConfigurationAction.RemoveRule:
                if (request.RuleId is null) throw new ArgumentException("Parameter 'rule_id' is required for this action.");
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                await client.RequireEntityType(request.RuleId.Value, "rule_id", "ParkingRule");
                return await client.ExecuteEntityMethod(
                    request.ZoneId.ToString(),
                    "RemoveParkingZoneRule",
                    request.RuleId.ToString());

            case ParkingZoneConfigurationAction.GetCapacity:
                await client.RequireEntityType(request.ZoneId, "zone_id", "ParkingZone");
                return await client.GetEntityField(request.ZoneId.ToString(), "Occupancy");

        default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum ParkingZoneConfigurationAction
{
    GetCameras,
    AddLprCamera, RemoveLprCamera,
    AddRule, RemoveRule,
    GetCapacity
}

public enum ParkingZoneLprCameraPosition
{
    Entrance,
    Exit
}

public enum ParkingZoneLprRelativeMotion
{
    Ignored,
    Approaching,
    MovingAway
}

public sealed record ParkingZoneConfigurationRequest(
    [property: Description("Parking zone GUID"), Required] Guid ZoneId,
    [property: Description("Action to perform"), Required] ParkingZoneConfigurationAction Action,
    [property: Description("LPR device GUID for get_cameras/add_lpr_camera, or LPR access point GUID for remove_lpr_camera")] Guid? CameraId = null,
    [property: Description("Parking rule GUID (required for add_rule, remove_rule)")] Guid? RuleId = null,
    [property: Description("Camera position (required for add_lpr_camera)")] ParkingZoneLprCameraPosition? Position = null,
    [property: Description("Relative motion (required for add_lpr_camera)")] ParkingZoneLprRelativeMotion? RelativeMotion = null);
