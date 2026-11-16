using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupMembersToolTests
{
    [Theory]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.IsMember)]
    public async Task MixedMemberBatch_PreservesValidResultAndWrongTypeFailure(UserGroupMembersAction action)
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(groupId, "UserGroup");
        handler.AddEntity(userId, "User");
        handler.AddEntity(wrongId, "Partition");
        var result = await UserGroupMembersTool.Execute(TestHelpers.FakeManager(handler),
            new UserGroupMembersRequest(action, groupId, MemberIds: [userId, wrongId]));

        if (action is UserGroupMembersAction.IsMember)
        {
            var results = Assert.IsType<UserMembershipCheckResult[]>(result);
            Assert.Equal(userId, results[0].MemberId);
            Assert.Null(results[0].Error);
            Assert.NotNull(results[0].Result);
            Assert.Equal(wrongId, results[1].MemberId);
            Assert.Contains("member_ids", results[1].Error);
        }
        else
        {
            var batch = Assert.IsType<Genetec.Mcp.SecurityCenter.Tools.BatchCreateResult>(result);
            Assert.Equal(2, batch.RequestedCount);
            Assert.Equal(1, batch.SuccessCount);
            Assert.Equal(1, batch.FailureCount);
            Assert.True(batch.Results[0].Success);
            Assert.Equal(userId, batch.Results[0].Guid);
            Assert.False(batch.Results[1].Success);
            Assert.Contains("member_ids", batch.Results[1].Error);
        }
        Assert.Equal(4, handler.Requests.Count);
        Assert.Single(handler.Requests, request =>
            Uri.UnescapeDataString(request.Uri.Query).Contains(wrongId.ToString(), StringComparison.Ordinal));
        Assert.Single(handler.Requests, request =>
            !Uri.UnescapeDataString(request.Uri.Query).EndsWith(",Guid,EntityType", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.IsMember)]
    [InlineData(UserGroupMembersAction.AddChild)]
    [InlineData(UserGroupMembersAction.RemoveChild)]
    public async Task RelatedEntityTargets_RejectWrongEntityType(UserGroupMembersAction action)
    {
        await TestHelpers.AssertWrongRelatedEntityTypeAsync("UserGroup", (manager, primaryId, relatedId) => UserGroupMembersTool.Execute(manager, new UserGroupMembersRequest(action, primaryId, ChildGroupId: relatedId, MemberIds: [relatedId])), action is UserGroupMembersAction.AddChild or UserGroupMembersAction.RemoveChild ? "child_group_id" : "member_ids", "Partition");
    }

    [Theory]
    [InlineData(UserGroupMembersAction.GetMembers)]
    [InlineData(UserGroupMembersAction.AddMember)]
    [InlineData(UserGroupMembersAction.RemoveMember)]
    [InlineData(UserGroupMembersAction.AddChild)]
    [InlineData(UserGroupMembersAction.RemoveChild)]
    [InlineData(UserGroupMembersAction.IsMember)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(UserGroupMembersAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => UserGroupMembersTool.Execute(manager, new UserGroupMembersRequest(action, id, ChildGroupId: Guid.NewGuid(), MemberIds: [Guid.NewGuid()])), "id", "Partition");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddMember_NullUserId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.AddMember, Id: Guid.NewGuid())));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveMember_NullUserId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.RemoveMember, Id: Guid.NewGuid())));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddChild_NullChildGroupId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.AddChild, Id: Guid.NewGuid())));

        Assert.Contains("child_group_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveChild_NullChildGroupId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.RemoveChild, Id: Guid.NewGuid())));

        Assert.Contains("child_group_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsMember_NullUserId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.IsMember, Id: Guid.NewGuid())));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMember_EmptyMemberIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupMembersTool.Execute(TestHelpers.FakeManager(),
                new UserGroupMembersRequest(UserGroupMembersAction.AddMember,
                    Id: Guid.NewGuid(), MemberIds: [])));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMember_ReturnsRawSdkResponse()
    {
        var handler = new Tools.ToolTestHandler();
        var metadata = new Tools.EntityMetadataTestHandler(handler);
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        metadata.AddEntity(groupId, "UserGroup");
        metadata.AddEntity(userId, "User");

        var result = await UserGroupMembersTool.Execute(TestHelpers.FakeManager(metadata),
            new UserGroupMembersRequest(UserGroupMembersAction.AddMember,
                Id: groupId, MemberIds: [userId]));

        Assert.Equal("{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}",
            Assert.IsType<string>(result));
    }
}
