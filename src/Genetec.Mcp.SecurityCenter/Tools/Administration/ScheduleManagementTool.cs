using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class ScheduleManagementTool
{
    [McpServerTool(
        Name = "schedule_management",
        Title = "Schedule Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage schedules: get, list, create, update, delete. " +
                "Create: supply items with names (single = single create, multiple = batch). " +
                "Use items for CRUD: one item is single, multiple items are batch.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Schedule management request")] ScheduleManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ScheduleAction.List:
                return await client.ListEntities("Schedule", request.PageSize, request.Page, returnFields: ["Name"]);

            case ScheduleAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntityTrimmed(request.Id.Value, "Schedule");

            case ScheduleAction.Create:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                var names = ManagementBatchItemHelper.GetCreateNames(items);
                if (items.Any(item => item.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var creates = items.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "Schedule", creates);
            }

            case ScheduleAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<ScheduleUpdateProperties>(client, items, "Schedule",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new ScheduleUpdateProperties(Name: name));
            }

            case ScheduleAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<ScheduleUpdateProperties>(items), "Schedule");
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }


}



public enum ScheduleAction
{
    Get,
    List,
    Create,
    Update,
    Delete
}

public sealed record ScheduleBatchItem(
    [property: Description("Schedule name for create or rename"), MinLength(1)] string? Name = null,
    [property: Description("Schedule GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed Schedule properties for update")] ScheduleUpdateProperties? Body = null) : IManagementCrudItem<ScheduleUpdateProperties>;

public sealed record ScheduleManagementRequest(
    [property: Description("Action to perform"), Required] ScheduleAction Action,
    [property: Description("Schedule GUID for get")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ScheduleBatchItem>? Items = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record ScheduleUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Coverage type")] string? CoverageType = null,
    [Description("Schedule coverage data as defined by the Web SDK")] string? Data = null,
    [Description("Schedule type")] string? ScheduleType = null,
    [Description("Whether the schedule is archiver-friendly")] bool? IsArchiverFriendly = null);
