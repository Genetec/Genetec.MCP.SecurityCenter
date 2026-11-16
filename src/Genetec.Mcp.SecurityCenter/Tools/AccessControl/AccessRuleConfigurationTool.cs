using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AccessRuleConfigurationTool
{
    [McpServerTool(
        Name = "access_rule_configuration",
        Title = "Access Rule Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Configure access rule membership, schedule, and deny mode. " +
        "Actions: get_cardholders, add_cardholder, remove_cardholder, get_access_points, " +
        "clear_cardholders, is_member, get_schedule, set_schedule, get_is_denying, " +
        "set_is_denying, add_cardholder_group, remove_cardholder_group. " +
        "add_cardholder/remove_cardholder and add_cardholder_group/remove_cardholder_group accept one or more IDs.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access rule configuration request")] AccessRuleConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        async Task<BatchCreateResult> BulkGroupOp(bool add)
        {
            ResourceLimits.EnsureBatchOperationCount(request.GroupIds!.Count, "group_ids");
            var operations = new List<(Guid Id, string? MemberId, string? Error, string? Warning)>();
            var processedGroupIds = new HashSet<Guid>();
            var processedMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mutationCount = 0;

            foreach (var gid in request.GroupIds!)
            {
                if (!processedGroupIds.Add(gid))
                    continue;

                string[] memberIds;
                try
                {
                    await client.RequireEntityType(gid, "group_ids", "CardholderGroup");
                    var membersRaw = await client.GetEntityField(gid.ToString(), "Members");
                    memberIds = CreateToolHelper.GuidRegex.Matches(membersRaw)
                        .Select(m => m.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                catch (Exception ex)
                {
                    operations.Add((gid, null, $"Failed to get members of group {gid}: {ex.Message}", null));
                    continue;
                }

                if (memberIds.Length == 0)
                {
                    operations.Add((gid, null, null, "Group has no members; nothing to apply."));
                }
                else
                {
                    foreach (var memberId in memberIds.Where(processedMemberIds.Add))
                    {
                        operations.Add((Guid.Parse(memberId), memberId, null, null));
                        ResourceLimits.EnsureBatchOperationCount(++mutationCount, "group_ids");
                    }
                }
            }

            var results = new List<BatchCreateResultItem>(operations.Count);
            for (var index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                if (operation.Error is not null)
                {
                    results.Add(new BatchCreateResultItem(index, false, Error: operation.Error));
                    continue;
                }
                if (operation.Warning is not null)
                {
                    results.Add(new BatchCreateResultItem(index, true, Guid: operation.Id, Warning: operation.Warning));
                    continue;
                }

                try
                {
                    var response = await client.ExecuteEntityMethod(
                        request.Id.ToString(), add ? "AddCardholders" : "RemoveCardholders", operation.MemberId);
                    results.Add(new BatchCreateResultItem(index, true, Guid: operation.Id, Response: response));
                }
                catch (Exception ex)
                {
                    results.Add(new BatchCreateResultItem(index, false, Guid: operation.Id, Error: ex.Message));
                }
            }

            var successCount = results.Count(item => item.Success);
            return new BatchCreateResult("batch", results.Count, successCount, results.Count - successCount, results);
        }

        switch (request.Action)
        {
            case AccessRuleConfigAction.GetCardholders:
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.GetEntityField(request.Id.ToString(), "Cardholders");

            case AccessRuleConfigAction.AddCardholder:
                if (request.CardholderIds is null || request.CardholderIds.Count == 0)
                    throw new ArgumentException("Parameter 'cardholder_ids' is required for add_cardholder.");
                ResourceLimits.EnsureBatchOperationCount(request.CardholderIds.Count, "cardholder_ids");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                foreach (var cardholderId in request.CardholderIds)
                    await client.RequireEntityType(cardholderId, "cardholder_ids", "Cardholder", "Visitor");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "AddCardholders",
                    $"{{{string.Join(',', request.CardholderIds)}}}");

            case AccessRuleConfigAction.RemoveCardholder:
                if (request.CardholderIds is null || request.CardholderIds.Count == 0)
                    throw new ArgumentException("Parameter 'cardholder_ids' is required for remove_cardholder.");
                ResourceLimits.EnsureBatchOperationCount(request.CardholderIds.Count, "cardholder_ids");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                foreach (var cardholderId in request.CardholderIds)
                    await client.RequireEntityType(cardholderId, "cardholder_ids", "Cardholder", "Visitor");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "RemoveCardholders",
                    $"{{{string.Join(',', request.CardholderIds)}}}");

            case AccessRuleConfigAction.GetAccessPoints:
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.GetEntityField(request.Id.ToString(), "RelatedAccessPoints");

            case AccessRuleConfigAction.ClearCardholders:
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.ExecuteEntityMethod(request.Id.ToString(), "ClearCardholders");

            case AccessRuleConfigAction.IsMember:
                if (request.CardholderIds is null || request.CardholderIds.Count == 0)
                    throw new ArgumentException("Parameter 'cardholder_ids' is required for is_member.");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                if (request.CardholderIds.Count == 1)
                {
                    await client.RequireEntityType(request.CardholderIds[0], "cardholder_ids", "Cardholder", "Visitor");
                    return await client.GetEntityField(request.Id.ToString(), $"IsMember({request.CardholderIds[0]})");
                }
                ResourceLimits.EnsureBatchOperationCount(request.CardholderIds.Count, "cardholder_ids");
                var memberChecks = await Task.WhenAll(request.CardholderIds.Select(async (cid, idx) =>
                {
                    try
                    {
                        await client.RequireEntityType(cid, "cardholder_ids", "Cardholder", "Visitor");
                        var r = await client.GetEntityField(request.Id.ToString(), $"IsMember({cid})");
                        return new CardholderMembershipCheckResult(idx, cid, r);
                    }
                    catch (Exception ex)
                    {
                        return new CardholderMembershipCheckResult(idx, cid, Error: ex.Message);
                    }
                }));
                return memberChecks;

            case AccessRuleConfigAction.GetSchedule:
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.GetEntityField(request.Id.ToString(), "Schedule");

            case AccessRuleConfigAction.SetSchedule:
                if (request.ScheduleId is null)
                    throw new ArgumentException("Parameter 'schedule_id' is required for set_schedule.");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                await client.RequireEntityType(request.ScheduleId.Value, "schedule_id", "Schedule");
                return await client.SetEntityField(request.Id.ToString(), "Schedule", request.ScheduleId!.ToString());

            case AccessRuleConfigAction.GetIsDenying:
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.GetEntityField(request.Id.ToString(), "IsDenyingRule");

            case AccessRuleConfigAction.SetIsDenying:
                if (request.IsDenyingRule is null)
                    throw new ArgumentException("Parameter 'is_denying_rule' is required for set_is_denying.");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await client.SetEntityField(request.Id.ToString(), "IsDenyingRule", request.IsDenyingRule.Value.ToString().ToLower());

            case AccessRuleConfigAction.AddCardholderGroup:
                if (request.GroupIds is null || request.GroupIds.Count == 0)
                    throw new ArgumentException("Parameter 'group_ids' is required for add_cardholder_group.");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await BulkGroupOp(add: true);

            case AccessRuleConfigAction.RemoveCardholderGroup:
                if (request.GroupIds is null || request.GroupIds.Count == 0)
                    throw new ArgumentException("Parameter 'group_ids' is required for remove_cardholder_group.");
                await client.RequireEntityType(request.Id, "id", "AccessRule");
                return await BulkGroupOp(add: false);

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum AccessRuleConfigAction
{
    GetCardholders,
    AddCardholder,
    RemoveCardholder,
    GetAccessPoints,
    ClearCardholders,
    IsMember,
    GetSchedule,
    SetSchedule,
    GetIsDenying,
    SetIsDenying,
    AddCardholderGroup,
    RemoveCardholderGroup
}

public sealed record AccessRuleConfigRequest(
    [property: Description("Action to perform"), Required] AccessRuleConfigAction Action,
    [property: Description("Access rule GUID"), Required] Guid Id,
    [property: Description("Cardholder GUIDs — one or more for add_cardholder, remove_cardholder, is_member"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? CardholderIds = null,
    [property: Description("Schedule GUID")] Guid? ScheduleId = null,
    [Description("true = deny rule, false = grant rule")] bool? IsDenyingRule = null,
    [property: Description("Cardholder group GUIDs — one or more for add_cardholder_group, remove_cardholder_group"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? GroupIds = null);

public sealed record CardholderMembershipCheckResult(int Index, Guid CardholderId, string? Result = null, string? Error = null);
