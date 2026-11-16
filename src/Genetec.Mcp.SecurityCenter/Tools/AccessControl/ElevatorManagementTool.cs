using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class ElevatorManagementTool
{
    [McpServerTool(
        Name = "elevator_management",
        Title = "Elevator Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage elevator entities for multi-floor access control: list, get, create, delete, update")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Elevator management request")] ElevatorManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ElevatorAction.List:
                return await client.ListEntities("Elevator", request.PageSize, request.Page);

            case ElevatorAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntity(request.Id.Value, "Elevator");

            case ElevatorAction.Create:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                var names = ManagementBatchItemHelper.GetCreateNames(items);
                var creates = items.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]), item.Body)).ToList();
                return await CreateToolHelper.Execute(client, "Elevator", creates);
            }

            case ElevatorAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<ElevatorUpdateProperties>(items), "Elevator");
            }

            case ElevatorAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<ElevatorUpdateProperties>(client, items, "Elevator",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) });
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum ElevatorAction
{
    Get,
    List,
    Create,
    Delete,
    Update
}



public sealed record ElevatorManagementItem(
    [property: Description("Elevator name for create"), MinLength(1)] string? Name = null,
    [property: Description("Elevator GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed Elevator properties for create or update")] ElevatorUpdateProperties? Body = null) : IManagementCrudItem<ElevatorUpdateProperties>;

public sealed record ElevatorManagementRequest(
    [property: Description("Action to perform"), Required] ElevatorAction Action,
    [property: Description("Elevator GUID for get")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ElevatorManagementItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record ElevatorUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Cabin reader GUID")] Guid? CabinReader = null,
    [property: Description("Access permission level"), Range(0, int.MaxValue)] int? AccessPermissionLevel = null,
    [property: Description("Elevator relay timeout in seconds"), Range(0, int.MaxValue)] int? ElevatorRelayTimeout = null,
    [Description("Interface module GUID")] Guid? InterfaceModule = null,
    [Description("Free access on normal output")] bool? IsFreeAccessOnNormalOutput = null,
    [Description("Main access control unit GUID")] Guid? MainUnit = null);
