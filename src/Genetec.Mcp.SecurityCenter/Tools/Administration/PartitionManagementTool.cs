using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class PartitionManagementTool
{
    [McpServerTool(
        Name = "partition_management",
        Title = "Partition Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
        Description("Manage partitions: get, list, create, update, delete, get_members, add_member, remove_member. " +
                "" +
                "Use items for CRUD: one item is single, multiple items are batch. Add or remove a member entity with member_id.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Partition management request")] PartitionManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case PartitionAction.List:
                return await client.ListEntities("Partition", request.PageSize, request.Page);

            case PartitionAction.Get:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get'.", "id");
                return await client.GetEntityTrimmed(request.Id.Value, "Partition");

            case PartitionAction.GetMembers:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'get_members'.");
                await client.RequireEntityType(request.Id.Value, "id", "Partition");
                return await client.GetEntityField(request.Id.Value.ToString(), "Members");

            case PartitionAction.Create:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                var names = ManagementBatchItemHelper.GetCreateNames(items);
                if (items.Any(item => item.Body is not null))
                    throw new ArgumentException("Parameter 'items.body' is only valid for update.", "items.body");
                var creates = items.Select((item, index) => new SecurityCenterRestClient.BatchCreateItem(
                    EntityNameSanitizer.Sanitize(names[index]))).ToList();
                return await CreateToolHelper.Execute(client, "Partition", creates);
            }

            case PartitionAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<PartitionUpdateProperties>(client, items, "Partition",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new PartitionUpdateProperties(Name: name));
            }

            case PartitionAction.AddMember:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'add_member'.");
                if (request.MemberId is null)
                    throw new ArgumentException("Parameter 'member_id' is required for action 'add_member'.");
                await client.RequireEntityType(request.Id.Value, "id", "Partition");
                return await client.ExecuteEntityMethod(request.Id.Value.ToString(), "AddMember", request.MemberId.Value.ToString());

            case PartitionAction.RemoveMember:
                if (request.Id is null)
                    throw new ArgumentException("Parameter 'id' is required for action 'remove_member'.");
                if (request.MemberId is null)
                    throw new ArgumentException("Parameter 'member_id' is required for action 'remove_member'.");
                await client.RequireEntityType(request.Id.Value, "id", "Partition");
                return await client.ExecuteEntityMethod(request.Id.Value.ToString(), "RemoveMember", request.MemberId.Value.ToString());

            case PartitionAction.Delete:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await DeleteToolHelper.Execute(client, ManagementToolHelper.DeleteIds<PartitionUpdateProperties>(items), "Partition");
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }


}

public enum PartitionAction
{
    Get,
    List,
    Create,
    Update,
    GetMembers,
    AddMember,
    RemoveMember,
    Delete
}

public sealed record PartitionManagementItem(
    [property: Description("Partition name for create or rename"), MinLength(1)] string? Name = null,
    [property: Description("Partition GUID for update or delete")] Guid? Id = null,
    [property: Description("Typed Partition properties for update")] PartitionUpdateProperties? Body = null) : IManagementCrudItem<PartitionUpdateProperties>;

public sealed record PartitionManagementRequest(
    [property: Description("Action to perform"), Required] PartitionAction Action,
    [property: Description("Partition GUID for get, get_members, add_member, or remove_member")] Guid? Id = null,
    [property: Description("One or more items for create, update, or delete"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<PartitionManagementItem>? Items = null,
    [Description("Member entity GUID (for add_member or remove_member)")] Guid? MemberId = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record PartitionUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Whether the partition can be shared")] bool? CanBeShared = null);
