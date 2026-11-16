using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupMembersToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.IsMember)]
    public async Task MixedMemberBatch_PreservesValidResultAndWrongTypeEntity(UserGroupMembersAction action)
    {
        var group = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var userName = $"MCP_TypeGuardUser_{Guid.NewGuid():N}";
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(userName)])));
        sc.TrackForCleanup(user.Guid);
        if (action is not UserGroupMembersAction.AddMember)
            await UserGroupMembersTool.Execute(sc.Manager,
                new UserGroupMembersRequest(UserGroupMembersAction.AddMember, group.Id, MemberIds: [user.Guid]));
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
        var result = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(action, group.Id, MemberIds: [user.Guid, wrong.Id]));
        if (action is UserGroupMembersAction.IsMember)
        {
            var results = Assert.IsType<UserMembershipCheckResult[]>(result);
            Assert.Equal(user.Guid, results[0].MemberId);
            Assert.Null(results[0].Error);
            var expected = Assert.IsType<string>(await UserGroupMembersTool.Execute(sc.Manager,
                new UserGroupMembersRequest(UserGroupMembersAction.IsMember, group.Id, MemberIds: [user.Guid])));
            Assert.Equal(expected, results[0].Result);
            Assert.Equal(wrong.Id, results[1].MemberId);
            Assert.Contains("member_ids", results[1].Error);
        }
        else
        {
            var batch = Assert.IsType<Genetec.Mcp.SecurityCenter.Tools.BatchCreateResult>(result);
            Assert.Equal(2, batch.RequestedCount);
            Assert.Equal(1, batch.SuccessCount);
            Assert.Equal(1, batch.FailureCount);
            Assert.True(batch.Results[0].Success);
            Assert.Equal(user.Guid, batch.Results[0].Guid);
            Assert.False(batch.Results[1].Success);
            Assert.Contains("member_ids", batch.Results[1].Error);
        }
        var members = await sc.Manager.GetClient().GetEntityField(group.Id.ToString(), "Members");
        if (action is UserGroupMembersAction.RemoveMember)
            Assert.DoesNotContain(user.Guid.ToString(), members, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Contains(user.Guid.ToString(), members, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(wrong.Id.ToString(), members, StringComparison.OrdinalIgnoreCase);
        await IntegrationTestHelpers.AssertEntityAsync(sc, group.Id, "UserGroup", group.Name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, user.Guid, "User", userName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    [Theory]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.IsMember)]
    [InlineData(UserGroupMembersAction.AddChild)]
    [InlineData(UserGroupMembersAction.RemoveChild)]
    public async Task RelatedEntityTargets_RejectWrongEntityTypeAndPreserveEntities(UserGroupMembersAction action)
    {
        var primaryId = (await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc)).Id;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, primaryId, "UserGroup", relatedId => UserGroupMembersTool.Execute(sc.Manager, new UserGroupMembersRequest(action, primaryId, ChildGroupId: relatedId, MemberIds: [relatedId])), action is UserGroupMembersAction.AddChild or UserGroupMembersAction.RemoveChild ? "child_group_id" : "member_ids", "Partition");
    }

    [Theory]
    [InlineData(UserGroupMembersAction.GetMembers)]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.AddChild)]
    [InlineData(UserGroupMembersAction.RemoveChild)]
    [InlineData(UserGroupMembersAction.IsMember)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(UserGroupMembersAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => UserGroupMembersTool.Execute(sc.Manager, new UserGroupMembersRequest(action, id, ChildGroupId: Guid.NewGuid(), MemberIds: [Guid.NewGuid()])), "id", "Partition");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_ExistingGroup_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_UG_{Guid.NewGuid():N}";
        var createResult = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.GetMembers, Id: created.Guid));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task AddAndRemoveMember_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        // Create group
        var groupName = $"MCP_UG_{Guid.NewGuid():N}";
        var groupResult = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(groupName)]));
        var group = Assert.IsType<EntityCreateResult>(groupResult);
        sc.TrackForCleanup(group.Guid);

        // Create user to add as member
        var userResult = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        var user = Assert.IsType<EntityCreateResult>(userResult);
        sc.TrackForCleanup(user.Guid);

        // Add member
        var addResult = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.AddMember,
                Id: group.Guid, MemberIds: [user.Guid]));
        Assert.IsType<string>(addResult);

        // IsMember
        var isMemberResult = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.IsMember,
                Id: group.Guid, MemberIds: [user.Guid]));
        Assert.NotNull(isMemberResult);

        // Remove member
        var removeResult = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.RemoveMember,
                Id: group.Guid, MemberIds: [user.Guid]));
        Assert.IsType<string>(removeResult);
    }

    [Fact]
    public async Task AddAndRemoveChild_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        // Create parent group
        var parentResult = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_Parent_{Guid.NewGuid():N}")]));
        var parent = Assert.IsType<EntityCreateResult>(parentResult);
        sc.TrackForCleanup(parent.Guid);

        // Create child group
        var childResult = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_Child_{Guid.NewGuid():N}")]));
        var child = Assert.IsType<EntityCreateResult>(childResult);
        sc.TrackForCleanup(child.Guid);

        // AddChild
        var addResult = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.AddChild,
                Id: parent.Guid, ChildGroupId: child.Guid));
        Assert.IsType<string>(addResult);

        // RemoveChild
        var removeResult = await UserGroupMembersTool.Execute(sc.Manager,
            new UserGroupMembersRequest(UserGroupMembersAction.RemoveChild,
                Id: parent.Guid, ChildGroupId: child.Guid));
        Assert.IsType<string>(removeResult);
    }
}
