using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaSecurityTool
{
    [McpServerTool(
        Name = "area_security",
        Title = "Area Security",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Control area security including arming, disarming, unlocking perimeter doors, " +
        "people counting, and access point operations. " +
        "Actions: arm, disarm, arm_perimeter, arm_zones, disarm_zones, " +
        "unlock_perimeter_doors, get_people_count, modify_people_count, " +
        "reset_people_count, forgive_antipassback, shunt_access_point_device")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area security request")] AreaSecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case AreaSecurityAction.Arm:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "Arm", request.Delay?.ToString());

            case AreaSecurityAction.Disarm:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "Disarm");

            case AreaSecurityAction.ArmPerimeter:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "ArmPerimeter", request.Delay?.ToString());

            case AreaSecurityAction.ArmZones:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "ArmZones");

            case AreaSecurityAction.DisarmZones:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "DisarmZones");

            case AreaSecurityAction.UnlockPerimeterDoors:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "UnlockAllPerimeterDoors");

            case AreaSecurityAction.GetPeopleCount:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "GetPeopleCount");

            case AreaSecurityAction.ModifyPeopleCount:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                if (request.CardholderId == null)
                    throw new ArgumentException("Parameter 'cardholder_id' is required for this action.");
                if (request.InAccess == null)
                    throw new ArgumentException("Parameter 'in_access' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                await client.RequireEntityType(request.CardholderId.Value, "cardholder_id", "Cardholder", "Visitor");
                return await client.ExecuteAction(
                    $"ModifyPeopleCount({request.Id},{request.CardholderId},{request.InAccess.Value.ToString().ToLowerInvariant()})");

            case AreaSecurityAction.ResetPeopleCount:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for this action.");
                await client.RequireEntityType(request.Id.Value, "id", "Area");
                return await client.ExecuteEntityMethod(request.Id!.ToString(), "ResetPeopleCount");

            case AreaSecurityAction.ForgiveAntipassback:
                if (request.AccessPointId == null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                await client.RequireEntityType(request.AccessPointId.Value, "access_point_id", "AccessPoint");
                return await client.ExecuteAction($"ForgiveAntipassback({request.AccessPointId})");

            case AreaSecurityAction.ShuntAccessPointDevice:
                if (request.AccessPointId == null)
                    throw new ArgumentException("Parameter 'access_point_id' is required for this action.");
                if (request.Shunt == null)
                    throw new ArgumentException("Parameter 'shunt' is required for this action.");
                await client.RequireEntityType(request.AccessPointId.Value, "access_point_id", "AccessPoint");
                return await client.ExecuteAction(
                    $"ShuntAccessPointDevice({request.AccessPointId},{request.Shunt.Value.ToString().ToLowerInvariant()})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum AreaSecurityAction
{
    Arm,
    Disarm,
    ArmPerimeter,
    ArmZones,
    DisarmZones,
    UnlockPerimeterDoors,
    GetPeopleCount,
    ModifyPeopleCount,
    ResetPeopleCount,
    ForgiveAntipassback,
    ShuntAccessPointDevice
}

public sealed record AreaSecurityRequest(
    [property: Description("Action to perform"), Required] AreaSecurityAction Action,
    [property: Description("Area GUID")] Guid? Id = null,
    [property: Description("Cardholder GUID (for modify_people_count)")] Guid? CardholderId = null,
    [Description("true = entering area, false = exiting (for modify_people_count)")] bool? InAccess = null,
    [Description("Arm delay in seconds")] int? Delay = null,
    [property: Description("Access point GUID (for forgive_antipassback, shunt_access_point_device)")] Guid? AccessPointId = null,
    [property: Description("true to shunt the access point device, false to unshunt it (for shunt_access_point_device)")] bool? Shunt = null);
