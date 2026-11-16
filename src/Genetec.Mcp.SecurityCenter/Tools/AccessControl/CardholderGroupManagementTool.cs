using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CardholderGroupManagementTool
{
    [McpServerTool(
        Name = "cardholder_group_management",
        Title = "Cardholder Group Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
    Description("Manage cardholder groups: get, list, create, update, delete, get_members, add_member, remove_member. " +
                "Groups can be nested: add_member accepts both cardholder GUIDs and cardholder group GUIDs via member_ids, allowing groups to contain sub-groups. " +
                "Create accepts one or more group names via items (single or batch). " +
                "Delete accepts a single id or a list of GUIDs via items. " +
                "add_member/remove_member accept one or more cardholder or cardholder group GUIDs via member_ids.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Cardholder group management request")] CardholderGroupManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        async Task<BatchCreateResult> BulkMemberOp(bool add)
        {
            var ids = request.MemberIds!;
            ResourceLimits.EnsureBatchOperationCount(ids.Count, "member_ids");
            var successCount = 0;
            var results = new List<BatchCreateResultItem>(ids.Count);
            for (var idx = 0; idx < ids.Count; idx++)
            {
                var mid = ids[idx];
                try
                {
                    await client.RequireEntityType(mid, "member_ids", "Cardholder", "CardholderGroup");
                    var response = add
                        ? await client.AddToCollection(request.Id!.ToString(), "Members", mid.ToString())
                        : await client.RemoveFromCollection(request.Id!.ToString(), "Members", mid.ToString());
                    results.Add(new BatchCreateResultItem(idx, true, Guid: mid, Response: response));
                    successCount++;
                }
                catch (Exception ex)
                {
                    results.Add(new BatchCreateResultItem(idx, false, Error: ex.Message));
                }
            }
            return new BatchCreateResult("batch", ids.Count, successCount, ids.Count - successCount, results);
        }

        switch (request.Action)
        {
            case CardholderGroupAction.List:
                return await client.ListEntities("CardholderGroup", request.PageSize, request.Page, returnFields: ["Name"]);

            case CardholderGroupAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.");
                return await client.GetEntityTrimmed(request.Id.Value, "CardholderGroup");

            case CardholderGroupAction.Create:
            {
                if (request.Items is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'items' is required for action 'create'.");
                var names = ManagementBatchItemHelper.GetCreateNames(request.Items);
                if (request.Items.Count == 1)
                    return await client.CreateEntity<EntityCreateResult>("CardholderGroup",
                        EntityNameSanitizer.Sanitize(names[0]));
                var batchItems = names
                    .Select(n => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(n)))
                    .ToList();
                var raw = await client.CreateEntitiesBatch("CardholderGroup", batchItems);
                return CreateToolHelper.BuildBatchResponse(raw, batchItems.Count);
            }

            case CardholderGroupAction.Update:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'update'.");
                if (request.Body is null)
                    throw new ArgumentException("Parameter 'body' is required for action 'update'.");
                await client.RequireEntityType(request.Id.Value, "id", "CardholderGroup");
                return await client.UpdateEntity(request.Id!.ToString(), request.Body);

            case CardholderGroupAction.Delete:
                var deleteItems = request.Items is { Count: > 0 }
                    ? ManagementBatchItemHelper.GetDeleteIds(request.Items)
                    : request.Id.HasValue
                        ? [request.Id.Value]
                        : throw new ArgumentException("Parameter 'id' or 'items' is required for delete.");
                return await DeleteToolHelper.Execute(client, deleteItems, "CardholderGroup");

            case CardholderGroupAction.GetMembers:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_members'.");
                await client.RequireEntityType(request.Id.Value, "id", "CardholderGroup");
                return await client.GetEntityField(request.Id!.ToString(), "Members");

            case CardholderGroupAction.AddMember:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'add_member'.");
                if (request.MemberIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'member_ids' is required for action 'add_member'.");
                await client.RequireEntityType(request.Id.Value, "id", "CardholderGroup");
                if (request.MemberIds.Count > 1)
                    return await BulkMemberOp(add: true);
                await client.RequireEntityType(request.MemberIds[0], "member_ids", "Cardholder", "CardholderGroup");
                return await client.AddToCollection(request.Id!.ToString(), "Members", request.MemberIds[0].ToString());

            case CardholderGroupAction.RemoveMember:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'remove_member'.");
                if (request.MemberIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'member_ids' is required for action 'remove_member'.");
                await client.RequireEntityType(request.Id.Value, "id", "CardholderGroup");
                if (request.MemberIds.Count > 1)
                    return await BulkMemberOp(add: false);
                await client.RequireEntityType(request.MemberIds[0], "member_ids", "Cardholder", "CardholderGroup");
                return await client.RemoveFromCollection(request.Id!.ToString(), "Members", request.MemberIds[0].ToString());

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }

}

public enum CardholderGroupAction
{
    Get,
    List,
    Create,
    Update,
    Delete,
    GetMembers,
    AddMember,
    RemoveMember
}

public sealed record CardholderGroupManagementRequest(
    [property: Description("Action to perform"), Required] CardholderGroupAction Action,
    [property: Description("Cardholder group GUID")] Guid? Id = null,
    [property: Description("For create, each item has a name; for delete, each item has an id"), MaxLength(ResourceLimits.MaxBatchItems)] List<CardholderGroupBatchItem>? Items = null,
    [property: Description("Cardholder or cardholder group GUIDs to add or remove (for add_member/remove_member) — pass a group GUID to nest groups"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? MemberIds = null,
    [Description("Typed cardholder group properties to update")] CardholderGroupUpdateProperties? Body = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1);

public sealed record CardholderGroupBatchItem(
    [property: Description("Group name for create")] string? Name = null,
    [property: Description("Cardholder group GUID for delete")] Guid? Id = null) : IManagementBatchItem;

public sealed record CardholderGroupUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Access permission level")] int? AccessPermissionLevel = null,
    [Description("Whether the group is exempt from antipassback")] bool? AntipassbackExemption = null,
    [Description("Whether antipassback exemption is inherited")] bool? AntipassbackExemptionIsInherited = null,
    [Description("Default badge template GUID")] Guid? DefaultBadgeTemplate = null,
    [Description("Group email address")] string? EmailAddress = null,
    [Description("Whether access permission level is inherited")] bool? InheritAccessPermissionLevelFromParentGroup = null,
    [Description("Whether this is a visitor group")] bool? IsVisitorGroup = null);
