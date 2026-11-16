using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaMembershipTool
{
    [McpServerTool(
        Name = "area_membership",
        Title = "Area Membership",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage area membership including child entities, cameras, doors, elevators, " +
        "access rules, and access points. " +
        "Actions: get_members, get_cameras, get_doors, get_elevators, get_access_rules, get_access_points, " +
                "add_member, remove_member, add_members_batch, remove_members_batch, " +
          "add_camera, remove_camera, add_cameras_batch, remove_cameras_batch, add_cameras_to_areas_batch, remove_cameras_from_areas_batch, " +
        "add_door, add_captive_door, remove_door, add_doors_batch, remove_doors_batch, " +
        "add_elevator, remove_elevator, add_elevators_batch, remove_elevators_batch, " +
        "add_access_rule, remove_access_rule, add_access_rules_batch, remove_access_rules_batch, " +
        "activity_report. Optionally provide time_start and time_end together to limit report events")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area membership request")] AreaMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        Task<BatchMethodResult> BulkLinkedOp(string method, string entityType)
            => client.ExecuteEntityMethodBatchOnSingleEntity(request.Id, method,
                request.Items!.Select(id => entityType == "AccessRule"
                    ? FormatAccessRuleArgument(id, request.DoorSide) : id.ToString()).ToList(),
                index => client.RequireEntityType(request.Items[index], "items", entityType));

        Task<BatchMethodResult> BulkCameraAreaOp(string method)
            => client.ExecuteEntityMethodBatch(method,
                request.CameraAreaPairs!.Select(pair => (pair.AreaId, (string?)pair.CameraId.ToString())).ToList(),
                async index =>
                {
                    var pair = request.CameraAreaPairs[index];
                    await client.RequireEntityType(pair.AreaId, "camera_area_pairs", "Area");
                    await client.RequireEntityType(pair.CameraId, "camera_area_pairs", "Camera");
                });

        switch (request.Action)
        {
            case AreaMembershipAction.GetMembers:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "CaptiveAreas");

            case AreaMembershipAction.GetCameras:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "Cameras");

            case AreaMembershipAction.GetDoors:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "AllDoors");

            case AreaMembershipAction.GetElevators:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "Elevators");

            case AreaMembershipAction.GetAccessRules:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "AccessRules");

            case AreaMembershipAction.GetAccessPoints:
                await client.RequireEntityType(request.Id, "id", "Area");
                return await client.GetEntityField(request.Id.ToString(), "EntryAccessPoints");

            case AreaMembershipAction.AddMember:
                if (request.MemberId == null)
                    throw new ArgumentException("Parameter 'member_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.MemberId.Value, "member_id", "Area");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddCaptiveArea", request.MemberId!.ToString());

            case AreaMembershipAction.RemoveMember:
                if (request.MemberId == null)
                    throw new ArgumentException("Parameter 'member_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.MemberId.Value, "member_id", "Area");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveCaptiveArea", request.MemberId!.ToString());

            case AreaMembershipAction.AddMembersBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("AddCaptiveArea", "Area");

            case AreaMembershipAction.RemoveMembersBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("RemoveCaptiveArea", "Area");

            case AreaMembershipAction.AddCamera:
                if (request.CameraId == null)
                    throw new ArgumentException("Parameter 'camera_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "Camera");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddCamera", request.CameraId!.ToString());

            case AreaMembershipAction.RemoveCamera:
                if (request.CameraId == null)
                    throw new ArgumentException("Parameter 'camera_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "Camera");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveCamera", request.CameraId!.ToString());

            case AreaMembershipAction.AddCamerasBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("AddCamera", "Camera");

            case AreaMembershipAction.RemoveCamerasBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("RemoveCamera", "Camera");

            case AreaMembershipAction.AddCamerasToAreasBatch:
                if (request.CameraAreaPairs is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'camera_area_pairs' is required for this action.");
                return await BulkCameraAreaOp("AddCamera");

            case AreaMembershipAction.RemoveCamerasFromAreasBatch:
                if (request.CameraAreaPairs is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'camera_area_pairs' is required for this action.");
                return await BulkCameraAreaOp("RemoveCamera");

            case AreaMembershipAction.AddDoor:
                if (request.DoorId == null)
                    throw new ArgumentException("Parameter 'door_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.DoorId.Value, "door_id", "Door");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddDoor", request.DoorId!.ToString());

            case AreaMembershipAction.AddCaptiveDoor:
                if (request.DoorId == null)
                    throw new ArgumentException("Parameter 'door_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.DoorId.Value, "door_id", "Door");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddCaptiveDoor", request.DoorId!.ToString());

            case AreaMembershipAction.RemoveDoor:
                if (request.DoorId == null)
                    throw new ArgumentException("Parameter 'door_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.DoorId.Value, "door_id", "Door");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveDoor", request.DoorId!.ToString());

            case AreaMembershipAction.AddDoorsBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("AddDoor", "Door");

            case AreaMembershipAction.RemoveDoorsBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("RemoveDoor", "Door");

            case AreaMembershipAction.AddElevator:
                if (request.ElevatorId == null)
                    throw new ArgumentException("Parameter 'elevator_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.ElevatorId.Value, "elevator_id", "Elevator");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddElevator", request.ElevatorId!.ToString());

            case AreaMembershipAction.RemoveElevator:
                if (request.ElevatorId == null)
                    throw new ArgumentException("Parameter 'elevator_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.ElevatorId.Value, "elevator_id", "Elevator");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveElevator", request.ElevatorId!.ToString());

            case AreaMembershipAction.AddElevatorsBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("AddElevator", "Elevator");

            case AreaMembershipAction.RemoveElevatorsBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("RemoveElevator", "Elevator");

            case AreaMembershipAction.AddAccessRule:
                if (request.AccessRuleId == null)
                    throw new ArgumentException("Parameter 'access_rule_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.AccessRuleId.Value, "access_rule_id", "AccessRule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddAccessRule",
                    FormatAccessRuleArgument(request.AccessRuleId.Value, request.DoorSide));

            case AreaMembershipAction.RemoveAccessRule:
                if (request.AccessRuleId == null)
                    throw new ArgumentException("Parameter 'access_rule_id' is required for this action.");
                await client.RequireEntityType(request.Id, "id", "Area");
                await client.RequireEntityType(request.AccessRuleId.Value, "access_rule_id", "AccessRule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveAccessRule",
                    FormatAccessRuleArgument(request.AccessRuleId.Value, request.DoorSide));

            case AreaMembershipAction.AddAccessRulesBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("AddAccessRule", "AccessRule");

            case AreaMembershipAction.RemoveAccessRulesBatch:
                if (request.Items is not { Count: > 0 })
                    throw new ArgumentException("Parameter 'items' is required for batch operations.");
                await client.RequireEntityType(request.Id, "id", "Area");
                return await BulkLinkedOp("RemoveAccessRule", "AccessRule");

            case AreaMembershipAction.ActivityReport:
                return await client.RunReport("AreaActivity",
                    ActivityReportQuery.WithTimeRange($"Areas@{request.Id}", request.TimeStart, request.TimeEnd));

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

    private static string FormatAccessRuleArgument(Guid accessRuleId, AreaDoorSide? doorSide)
        => doorSide is null ? accessRuleId.ToString() : $"{accessRuleId}\\,{doorSide.Value}";
}

public enum AreaDoorSide
{
    Entry,
    Exit,
    Both
}

public enum AreaMembershipAction
{
    GetMembers,
    GetCameras,
    GetDoors,
    GetElevators,
    GetAccessRules,
    GetAccessPoints,
    AddMember,
    RemoveMember,
    AddMembersBatch,
    RemoveMembersBatch,
    AddCamera,
    RemoveCamera,
    AddCamerasBatch,
    RemoveCamerasBatch,
    AddCamerasToAreasBatch,
    RemoveCamerasFromAreasBatch,
    AddDoor,
    AddCaptiveDoor,
    RemoveDoor,
    AddDoorsBatch,
    RemoveDoorsBatch,
    AddElevator,
    RemoveElevator,
    AddElevatorsBatch,
    RemoveElevatorsBatch,
    AddAccessRule,
    RemoveAccessRule,
    AddAccessRulesBatch,
    RemoveAccessRulesBatch,
    ActivityReport
}

public sealed record AreaCameraPair(
    [property: Description("Area GUID"), Required] Guid AreaId,
    [property: Description("Camera GUID"), Required] Guid CameraId);

public sealed record AreaMembershipRequest(
    [property: Description("Action to perform"), Required] AreaMembershipAction Action,
    [property: Description("Area GUID"), Required] Guid Id,
    [property: Description("Captive area GUID")] Guid? MemberId = null,
    [property: Description("Camera GUID")] Guid? CameraId = null,
    [property: Description("Door GUID")] Guid? DoorId = null,
    [property: Description("Elevator GUID")] Guid? ElevatorId = null,
    [property: Description("Access rule GUID")] Guid? AccessRuleId = null,
    [Description("Door side for add_access_rule, remove_access_rule, add_access_rules_batch, and remove_access_rules_batch; applies to every access rule in the batch")] AreaDoorSide? DoorSide = null,
    [property: Description("Entity GUIDs for batch operations; access rule batch actions can also use door_side for each item"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Items = null,
    [property: Description("List of area+camera pairs for multi-area camera ops"), MaxLength(ResourceLimits.MaxBatchItems)] List<AreaCameraPair>? CameraAreaPairs = null,
    [property: Description("Activity report start time (ISO 8601); provide with time_end"), Iso8601] string? TimeStart = null,
    [property: Description("Activity report end time (ISO 8601); provide with time_start"), Iso8601] string? TimeEnd = null);
