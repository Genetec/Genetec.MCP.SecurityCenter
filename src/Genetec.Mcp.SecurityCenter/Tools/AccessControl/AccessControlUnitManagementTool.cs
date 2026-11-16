using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessControlUnitManagementTool
{
    [McpServerTool(
        Name = "access_control_unit_management",
        Title = "Access Control Unit Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage access control units: get, list, update, get_doors, get_devices, get_interface_modules, add_interface_module, move")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access control unit management request")] AcuManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case AcuAction.List:
                return await client.ListEntities("Unit", request.PageSize, request.Page);

            case AcuAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.");
                return await client.GetEntity(request.Id.Value, "Unit");

            case AcuAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<AccessControlUnitUpdateProperties>(client, items, "Unit",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new AccessControlUnitUpdateProperties(Name: name));
            }

            case AcuAction.GetDoors:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_doors'.");
                await client.RequireEntityType(request.Id.Value, "id", "Unit");
                return await client.GetEntityField(request.Id!.ToString(), "Doors");

            case AcuAction.GetDevices:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_devices'.");
                await client.RequireEntityType(request.Id.Value, "id", "Unit");
                return await client.GetEntityField(request.Id!.ToString(), "Devices");

            case AcuAction.GetInterfaceModules:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_interface_modules'.");
                await client.RequireEntityType(request.Id.Value, "id", "Unit");
                return await client.GetEntityField(request.Id!.ToString(), "InterfaceModules");

            case AcuAction.AddInterfaceModule:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'add_interface_module'.");
                if (request.InterfaceModuleId is null) throw new ArgumentException("Parameter 'interface_module_id' is required for action 'add_interface_module'.");
                await client.RequireEntityType(request.Id.Value, "id", "Unit");
                await client.RequireEntityType(request.InterfaceModuleId.Value, "interface_module_id", "InterfaceModule");
                return await client.AddToCollection(request.Id!.ToString(), "InterfaceModules", request.InterfaceModuleId!.ToString());

            case AcuAction.Move:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'move'.");
                if (request.TargetUnitId is null) throw new ArgumentException("Parameter 'target_unit_id' is required for action 'move'.");
                await client.RequireEntityType(request.Id.Value, "id", "Unit");
                await client.RequireEntityType(request.TargetUnitId.Value, "target_unit_id", "Role");
                return await client.ExecuteAction($"MoveAccessControlUnit({request.Id},{request.TargetUnitId})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum AcuAction
{
    Get,
    List,
    Update,
    GetDoors,
    GetDevices,
    GetInterfaceModules,
    AddInterfaceModule,
    Move
}

public sealed record AcuManagementItem(
    [property: Description("Unit name for rename"), MinLength(1)] string? Name = null,
    [property: Description("Unit GUID for update")] Guid? Id = null,
    [property: Description("Typed Unit properties for update")] AccessControlUnitUpdateProperties? Body = null) : IManagementCrudItem<AccessControlUnitUpdateProperties>;

public sealed record AcuManagementRequest(
    [property: Description("Action to perform"), Required] AcuAction Action,
    [property: Description("Access control unit GUID for reads, add_interface_module, or move")] Guid? Id = null,
    [property: Description("Interface module GUID (for add_interface_module)")] Guid? InterfaceModuleId = null,
    [property: Description("Destination Access Manager role GUID (for move)")] Guid? TargetUnitId = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<AcuManagementItem>? Items = null);

public sealed record AccessControlUnitUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("AC failure state")] string? AcFail = null,
    [Description("Battery failure state")] string? BatteryFail = null,
    [Description("Extension type")] string? ExtensionType = null,
    [Description("Offline synchronization expiration")] DateTime? OfflineSyncExpiration = null,
    [Description("Offline synchronization frequency")] string? OfflineSyncFrequency = null,
    [Description("Offline synchronization mode")] string? OfflineSyncMode = null,
    [Description("Offline synchronization schedule time")] TimeSpan? OfflineSyncScheduleTime = null,
    [property: Description("Offline synchronization schedule times"), MaxLength(ResourceLimits.MaxBatchItems)] List<TimeSpan>? OfflineSyncScheduleTimes = null,
    [Description("Push register values on next connection")] bool? PushRegisterValuesOnNextConnect = null,
    [Description("Security protocol")] string? SecurityProtocol = null,
    [Description("Hardware tamper state")] string? HardwareTamper = null,
    [Description("Support elevator control")] bool? SupportElevatorControl = null,
    [Description("Support I/O linking")] bool? SupportIOLinking = null);
