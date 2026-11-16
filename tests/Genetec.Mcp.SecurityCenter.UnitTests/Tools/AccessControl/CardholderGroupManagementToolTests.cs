using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CardholderGroupManagementToolTests
{
    [Theory]
    [InlineData(CardholderGroupAction.Create)]
    [InlineData(CardholderGroupAction.Delete)]
    public async Task Batch_NullItemInProductionRequest_RejectsBeforeSdk(CardholderGroupAction action)
    {
        var json = "{\"action\":\"" + action.ToString().ToLowerInvariant() + "\",\"items\":[null]}";
        var request = JsonSerializer.Deserialize<CardholderGroupManagementRequest>(
            json, NumericStringConverter.ToolOptions)!;
        var handler = new EntityMetadataTestHandler();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGroupManagementTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal("items", error.ParamName);
        Assert.Contains("index 0", error.Message);
        Assert.Empty(handler.Requests);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(),
                new CardholderGroupManagementRequest(CardholderGroupAction.Create)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(),
                new CardholderGroupManagementRequest(CardholderGroupAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingBody_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(),
                new CardholderGroupManagementRequest(CardholderGroupAction.Update,
                    Id: Guid.NewGuid())));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMember_NullMemberIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(),
                new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                    Id: Guid.NewGuid())));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMember_EmptyMemberIds_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(),
                new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                    Id: Guid.NewGuid(), MemberIds: [])));

        Assert.Contains("member_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMember_ReturnsRawSdkResponse()
    {
        var handler = new EntityMetadataTestHandler();
        var groupId = Guid.NewGuid();
        var cardholderId = Guid.NewGuid();
        handler.AddEntity(groupId, "CardholderGroup");
        handler.AddEntity(cardholderId, "Cardholder");

        var result = await CardholderGroupManagementTool.Execute(TestHelpers.FakeManager(handler),
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: groupId, MemberIds: [cardholderId]));

        Assert.Equal("{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}",
            Assert.IsType<string>(result));
    }

    [Theory]
    [InlineData(CardholderGroupAction.Get)]
    [InlineData(CardholderGroupAction.Update)]
    [InlineData(CardholderGroupAction.Delete)]
    [InlineData(CardholderGroupAction.GetMembers)]
    [InlineData(CardholderGroupAction.AddMember)]
    [InlineData(CardholderGroupAction.RemoveMember)]
    public async Task TargetActions_WrongEntityType_RejectBeforeReadOrMutation(CardholderGroupAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action switch
        {
            CardholderGroupAction.Update => new CardholderGroupManagementRequest(action, Id: id,
                Body: new CardholderGroupUpdateProperties(Name: "Should not update")),
            CardholderGroupAction.AddMember or CardholderGroupAction.RemoveMember =>
                new CardholderGroupManagementRequest(action, Id: id, MemberIds: [Guid.NewGuid()]),
            _ => new CardholderGroupManagementRequest(action, Id: id)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGroupManagementTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(action == CardholderGroupAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(CardholderGroupAction.AddMember)]
    [InlineData(CardholderGroupAction.RemoveMember)]
    public async Task MemberActions_WrongMemberType_RejectSingleMember(CardholderGroupAction action)
    {
        var groupId = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(groupId, "CardholderGroup");
        handler.AddEntity(userGroupId, "UserGroup");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGroupManagementTool.Execute(
            TestHelpers.FakeManager(handler),
            new CardholderGroupManagementRequest(action, Id: groupId, MemberIds: [userGroupId])));

        Assert.Equal("member_ids", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData(CardholderGroupAction.AddMember)]
    [InlineData(CardholderGroupAction.RemoveMember)]
    public async Task MemberActions_MixedBatch_PreservesValidMemberAndRejectsWrongType(CardholderGroupAction action)
    {
        var groupId = Guid.NewGuid();
        var cardholderId = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(groupId, "CardholderGroup");
        handler.AddEntity(cardholderId, "Cardholder");
        handler.AddEntity(userGroupId, "UserGroup");

        var result = Assert.IsType<BatchCreateResult>(await CardholderGroupManagementTool.Execute(
            TestHelpers.FakeManager(handler),
            new CardholderGroupManagementRequest(action, Id: groupId, MemberIds: [cardholderId, userGroupId])));

        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);
        Assert.Equal(1, handler.Requests.Count(observed => observed.Method == HttpMethod.Post));
    }
}
