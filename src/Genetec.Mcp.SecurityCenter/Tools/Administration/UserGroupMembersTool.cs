using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class UserGroupMembersTool
{
    [McpServerTool(
        Name = "user_group_members",
        Title = "User Group Members",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage user group membership and hierarchy. " +
        "Use when listing members, adding or removing users, managing child groups, or checking membership.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("User group members request")] UserGroupMembersRequest request,
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
                    await client.RequireEntityType(mid, "member_ids", "User");
                    var response = add
                        ? await client.AddToCollection(request.Id.ToString(), "Members", mid.ToString())
                        : await client.RemoveFromCollection(request.Id.ToString(), "Members", mid.ToString());
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
            case UserGroupMembersAction.GetMembers:
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                return await client.GetEntityField(request.Id.ToString(), "Members");

            case UserGroupMembersAction.AddMember:
                if (request.MemberIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'member_ids' is required for action 'add_member'.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                if (request.MemberIds.Count > 1)
                    return await BulkMemberOp(add: true);
                await client.RequireEntityType(request.MemberIds[0], "member_ids", "User");
                return await client.AddToCollection(request.Id.ToString(), "Members", request.MemberIds[0].ToString());

            case UserGroupMembersAction.RemoveMember:
                if (request.MemberIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'member_ids' is required for action 'remove_member'.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                if (request.MemberIds.Count > 1)
                    return await BulkMemberOp(add: false);
                await client.RequireEntityType(request.MemberIds[0], "member_ids", "User");
                return await client.RemoveFromCollection(request.Id.ToString(), "Members", request.MemberIds[0].ToString());

            case UserGroupMembersAction.AddChild:
                if (request.ChildGroupId is null)
                    throw new ArgumentException("Parameter 'child_group_id' is required for this action.", "child_group_id");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                await client.RequireEntityType(request.ChildGroupId.Value, "child_group_id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddChild", request.ChildGroupId!.ToString());

            case UserGroupMembersAction.RemoveChild:
                if (request.ChildGroupId is null)
                    throw new ArgumentException("Parameter 'child_group_id' is required for this action.", "child_group_id");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                await client.RequireEntityType(request.ChildGroupId.Value, "child_group_id", "UserGroup");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveChild", request.ChildGroupId!.ToString());

            case UserGroupMembersAction.IsMember:
                if (request.MemberIds is null or { Count: 0 })
                    throw new ArgumentException("Parameter 'member_ids' is required for action 'is_member'.");
                await client.RequireEntityType(request.Id, "id", "UserGroup");
                if (request.MemberIds.Count == 1)
                {
                    await client.RequireEntityType(request.MemberIds[0], "member_ids", "User");
                    return await client.GetEntityField(request.Id.ToString(), $"IsMember({request.MemberIds[0]})");
                }
                ResourceLimits.EnsureBatchOperationCount(request.MemberIds.Count, "member_ids");
                var memberChecks = await Task.WhenAll(request.MemberIds.Select(async (mid, idx) =>
                {
                    try
                    {
                        await client.RequireEntityType(mid, "member_ids", "User");
                        var r = await client.GetEntityField(request.Id.ToString(), $"IsMember({mid})");
                        return new UserMembershipCheckResult(idx, mid, r);
                    }
                    catch (Exception ex)
                    {
                        return new UserMembershipCheckResult(idx, mid, Error: ex.Message);
                    }
                }));
                return memberChecks;

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }
}

public enum UserGroupMembersAction
{
    GetMembers,
    AddMember,
    RemoveMember,
    AddChild,
    RemoveChild,
    IsMember
}

public sealed record UserGroupMembersRequest(
    [property: Description("Action to perform"), Required] UserGroupMembersAction Action,
    [property: Description("User group GUID"), Required] Guid Id,
    [property: Description("Child group GUID (for add_child, remove_child)")] Guid? ChildGroupId = null,
    [property: Description("User GUIDs for add_member, remove_member, or is_member"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? MemberIds = null);

public sealed record UserMembershipCheckResult(int Index, Guid MemberId, string? Result = null, string? Error = null);
