using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessRuleConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task IsMember_MixedTypes_KeepsMatchingMemberResultAndRejectsWrongType()
    {
        var rule = await CreateRuleAsync();
        var cardholder = await CreateCardholderAsync();
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        await AccessRuleConfigurationTool.Execute(sc.Manager,
            new AccessRuleConfigRequest(AccessRuleConfigAction.AddCardholder, rule, CardholderIds: [cardholder]));
        var result = await AccessRuleConfigurationTool.Execute(sc.Manager,
            new AccessRuleConfigRequest(AccessRuleConfigAction.IsMember, rule, CardholderIds: [cardholder, wrong.Id]));
        var rows = Assert.IsType<CardholderMembershipCheckResult[]>(result);
        Assert.Equal(cardholder, rows[0].CardholderId);
        Assert.Null(rows[0].Error);
        Assert.Contains("true", rows[0].Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(wrong.Id, rows[1].CardholderId);
        Assert.Contains("UserGroup", rows[1].Error);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
        var membership = Assert.IsType<string>(await AccessRuleConfigurationTool.Execute(sc.Manager,
            new AccessRuleConfigRequest(AccessRuleConfigAction.GetCardholders, rule)));
        Assert.Contains(cardholder, IntegrationTestHelpers.ExtractGuids(membership));
        Assert.DoesNotContain(wrong.Id, IntegrationTestHelpers.ExtractGuids(membership));
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholder, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.RemoveCardholder, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.IsMember, "cardholder_ids")]
    [InlineData(AccessRuleConfigAction.SetSchedule, "schedule_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(AccessRuleConfigAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await CreateRuleAsync();
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "AccessRule", wrong => (action, parameter) switch
        {
            (AccessRuleConfigAction.AddCardholder, "cardholder_ids") => AccessRuleConfigurationTool.Execute(sc.Manager, new AccessRuleConfigRequest(action, id, CardholderIds: [wrong])),
            (AccessRuleConfigAction.RemoveCardholder, "cardholder_ids") => AccessRuleConfigurationTool.Execute(sc.Manager, new AccessRuleConfigRequest(action, id, CardholderIds: [wrong])),
            (AccessRuleConfigAction.IsMember, "cardholder_ids") => AccessRuleConfigurationTool.Execute(sc.Manager, new AccessRuleConfigRequest(action, id, CardholderIds: [wrong])),
            (AccessRuleConfigAction.SetSchedule, "schedule_id") => AccessRuleConfigurationTool.Execute(sc.Manager, new AccessRuleConfigRequest(action, id, ScheduleId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AccessRuleConfigAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(AccessRuleConfigAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleConfigurationTool.Execute(sc.Manager,
            new AccessRuleConfigRequest(action, Id: id, CardholderIds: [id], ScheduleId: id, IsDenyingRule: true, GroupIds: [id])));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetCardholders_ReturnsCardholderCollection()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();

        var result = await ExecuteAsync(AccessRuleConfigAction.GetCardholders, ruleId);

        var json = Assert.IsType<string>(result);
        Assert.Contains("Cardholders", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCardholder_SupportsMultipleCardholders()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var cardholder1 = await CreateCardholderAsync();
        var cardholder2 = await CreateCardholderAsync();

        var result = await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId,
            CardholderIds: [cardholder1, cardholder2]);

        Assert.Contains("true", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.Contains(cardholder1.ToString(), members, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cardholder2.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCardholder_SupportsMultipleCardholders()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var cardholder1 = await CreateCardholderAsync();
        var cardholder2 = await CreateCardholderAsync();

        await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId,
            CardholderIds: [cardholder1, cardholder2]);
        var result = await ExecuteAsync(AccessRuleConfigAction.RemoveCardholder, ruleId,
            CardholderIds: [cardholder1, cardholder2]);

        Assert.Contains("true", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.DoesNotContain(cardholder1.ToString(), members, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cardholder2.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccessPoints_ReturnsAccessPointCollection()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();

        var result = await ExecuteAsync(AccessRuleConfigAction.GetAccessPoints, ruleId);

        var json = Assert.IsType<string>(result);
        Assert.Contains("RelatedAccessPoints", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearCardholders_RemovesAllCardholders()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var cardholderId = await CreateCardholderAsync();

        await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId,
            CardholderIds: [cardholderId]);
        var result = await ExecuteAsync(AccessRuleConfigAction.ClearCardholders, ruleId);

        Assert.NotNull(result);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.DoesNotContain(cardholderId.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsMember_SupportsSingleAndMultipleCardholders()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var memberId = await CreateCardholderAsync();
        var nonMemberId = await CreateCardholderAsync();

        var before = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.IsMember, ruleId, CardholderIds: [memberId]));
        Assert.Contains("false", before, StringComparison.OrdinalIgnoreCase);

        await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId,
            CardholderIds: [memberId]);
        var after = await ExecuteAsync(AccessRuleConfigAction.IsMember, ruleId,
            CardholderIds: [memberId, nonMemberId]);
        var results = Assert.IsType<CardholderMembershipCheckResult[]>(after);
        Assert.Equal(memberId, results[0].CardholderId);
        Assert.Contains("true", results[0].Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nonMemberId, results[1].CardholderId);
        Assert.Contains("false", results[1].Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSchedule_ReturnsAssignedSchedule()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var scheduleId = await CreateScheduleAsync();
        await ExecuteAsync(AccessRuleConfigAction.SetSchedule, ruleId,
            ScheduleId: scheduleId);

        var result = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetSchedule, ruleId));

        Assert.Contains(scheduleId.ToString(), result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetSchedule_AssignsSchedule()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var scheduleId = await CreateScheduleAsync();

        var result = await ExecuteAsync(AccessRuleConfigAction.SetSchedule, ruleId,
            ScheduleId: scheduleId);

        Assert.NotNull(result);
        var stored = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetSchedule, ruleId));
        Assert.Contains(scheduleId.ToString(), stored, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIsDenying_ReturnsBooleanValue()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();

        var result = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetIsDenying, ruleId));

        Assert.Contains("IsDenyingRule", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetIsDenying_UpdatesBooleanValue()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();

        var result = await ExecuteAsync(AccessRuleConfigAction.SetIsDenying, ruleId,
            IsDenyingRule: true);

        Assert.NotNull(result);
        var stored = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetIsDenying, ruleId));
        Assert.Contains("true", stored, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCardholderGroup_AddsAllGroupMembers()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var cardholderId = await CreateCardholderAsync();
        var groupId = await CreateCardholderGroupAsync();

        await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: groupId, MemberIds: [cardholderId]));

        var result = await ExecuteAsync(AccessRuleConfigAction.AddCardholderGroup, ruleId,
            GroupIds: [groupId]);

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(1, batch.SuccessCount);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.Contains(cardholderId.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCardholderGroup_RemovesAllGroupMembers()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var cardholderId = await CreateCardholderAsync();
        var groupId = await CreateCardholderGroupAsync();

        await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: groupId, MemberIds: [cardholderId]));
        await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId,
            CardholderIds: [cardholderId]);

        var result = await ExecuteAsync(AccessRuleConfigAction.RemoveCardholderGroup, ruleId,
            GroupIds: [groupId]);

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(1, batch.SuccessCount);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.DoesNotContain(cardholderId.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_DeduplicateRepeatedGroupsAndOverlappingMembers(
        AccessRuleConfigAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var firstCardholderId = await CreateCardholderAsync();
        var sharedCardholderId = await CreateCardholderAsync();
        var lastCardholderId = await CreateCardholderAsync();
        var firstGroupId = await CreateCardholderGroupAsync();
        var secondGroupId = await CreateCardholderGroupAsync();

        await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: firstGroupId, MemberIds: [firstCardholderId, sharedCardholderId]));
        await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: secondGroupId, MemberIds: [sharedCardholderId, lastCardholderId]));

        var cardholderIds = new[] { firstCardholderId, sharedCardholderId, lastCardholderId };
        if (action == AccessRuleConfigAction.RemoveCardholderGroup)
            await ExecuteAsync(AccessRuleConfigAction.AddCardholder, ruleId, CardholderIds: cardholderIds.ToList());

        var result = await ExecuteAsync(action, ruleId,
            GroupIds: [firstGroupId, firstGroupId, secondGroupId, secondGroupId]);

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(3, batch.RequestedCount);
        Assert.Equal(3, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        foreach (var cardholderId in cardholderIds)
            Assert.Single(batch.Results, item => item.Guid == cardholderId);

        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        foreach (var cardholderId in cardholderIds)
        {
            if (action == AccessRuleConfigAction.AddCardholderGroup)
                Assert.Contains(cardholderId.ToString(), members, StringComparison.OrdinalIgnoreCase);
            else
                Assert.DoesNotContain(cardholderId.ToString(), members, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GroupActions_FiftyMembersAndEmptyGroup_AddAndRemoveAllMembers()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var groupId = await CreateCardholderGroupAsync();
        var emptyGroupId = await CreateCardholderGroupAsync();
        var created = Assert.IsType<BatchCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest(Enumerable.Range(0, 50)
                .Select(index => new CardholderItem($"MCP_ARBudget_{index}_{Guid.NewGuid():N}")).ToList())));
        foreach (var item in created.Results.Where(item => item.Success && item.Guid.HasValue))
            sc.TrackForCleanup(item.Guid!.Value);
        Assert.Equal(50, created.SuccessCount);
        Assert.Equal(0, created.FailureCount);
        var memberIds = created.Results.Select(item => Assert.IsType<Guid>(item.Guid)).ToList();
        await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember, Id: groupId, MemberIds: memberIds));

        foreach (var action in new[] { AccessRuleConfigAction.AddCardholderGroup, AccessRuleConfigAction.RemoveCardholderGroup })
        {
            var batch = Assert.IsType<BatchCreateResult>(await ExecuteAsync(action, ruleId, GroupIds: [groupId, emptyGroupId]));
            Assert.Equal(51, batch.RequestedCount);
            Assert.Equal(51, batch.SuccessCount);
            Assert.Equal(0, batch.FailureCount);
            Assert.Equal(Enumerable.Range(0, 51), batch.Results.Select(item => item.Index));
            Assert.Equal(emptyGroupId, batch.Results[50].Guid);
            Assert.Contains("Group has no members", batch.Results[50].Warning, StringComparison.Ordinal);
            foreach (var memberId in memberIds)
                Assert.Single(batch.Results.Take(50), item => item.Guid == memberId && item.Success);
            var members = Assert.IsType<string>(await ExecuteAsync(AccessRuleConfigAction.GetCardholders, ruleId));
            foreach (var memberId in memberIds)
            {
                if (action == AccessRuleConfigAction.AddCardholderGroup)
                    Assert.Contains(memberId.ToString(), members, StringComparison.OrdinalIgnoreCase);
                else
                    Assert.DoesNotContain(memberId.ToString(), members, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData(AccessRuleConfigAction.AddCardholderGroup)]
    [InlineData(AccessRuleConfigAction.RemoveCardholderGroup)]
    public async Task GroupActions_WrongEntityType_ReturnsFailureWithoutChangingRule(
        AccessRuleConfigAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ruleId = await CreateRuleAsync();
        var userGroupId = await CreateUserGroupAsync();

        var result = await ExecuteAsync(action, ruleId, GroupIds: [userGroupId]);

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(1, batch.RequestedCount);
        Assert.Equal(0, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.Contains("CardholderGroup", batch.Results[0].Error, StringComparison.Ordinal);
        Assert.Contains("UserGroup", batch.Results[0].Error, StringComparison.Ordinal);
        var members = Assert.IsType<string>(await ExecuteAsync(
            AccessRuleConfigAction.GetCardholders, ruleId));
        Assert.DoesNotContain(userGroupId.ToString(), members, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<object> ExecuteAsync(
        AccessRuleConfigAction action,
        Guid ruleId,
        List<Guid>? CardholderIds = null,
        Guid? ScheduleId = null,
        bool? IsDenyingRule = null,
        List<Guid>? GroupIds = null)
        => await AccessRuleConfigurationTool.Execute(sc.Manager,
            new AccessRuleConfigRequest(action, ruleId, CardholderIds,
                ScheduleId, IsDenyingRule, GroupIds));

    private async Task<Guid> CreateRuleAsync()
    {
        var result = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([
                new AccessRuleItem($"MCP_ARConfig_Rule_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<Guid> CreateCardholderAsync()
    {
        var result = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([
                new CardholderItem($"MCP_ARConfig_Cardholder_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<Guid> CreateCardholderGroupAsync()
    {
        var result = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Create,
                Items: [new CardholderGroupBatchItem(Name: $"MCP_ARConfig_Group_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<Guid> CreateUserGroupAsync()
    {
        var result = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([
                new UserGroupItem($"MCP_ARConfig_UserGroup_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<Guid> CreateScheduleAsync()
    {
        var result = await ScheduleManagementTool.Execute(sc.Manager,
            new ScheduleManagementRequest(ScheduleAction.Create,
                Items: [new ScheduleBatchItem(Name: $"MCP_ARConfig_Schedule_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

}
