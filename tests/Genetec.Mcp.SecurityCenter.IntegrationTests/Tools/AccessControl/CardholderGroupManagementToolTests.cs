using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderGroupManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CardholderGroupAction.Create)]
    [InlineData(CardholderGroupAction.Delete)]
    public async Task Batch_NullItem_RejectsBeforeMutation(CardholderGroupAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_NullBatch_{Guid.NewGuid():N}";
        Guid? existingId = null;
        if (action == CardholderGroupAction.Delete)
        {
            var created = Assert.IsType<EntityCreateResult>(await CardholderGroupManagementTool.Execute(sc.Manager,
                new CardholderGroupManagementRequest(CardholderGroupAction.Create, Items: [new CardholderGroupBatchItem(Name: name)])));
            existingId = created.Guid;
            sc.TrackForCleanup(created.Guid);
        }

        var firstItem = action == CardholderGroupAction.Create
            ? new CardholderGroupBatchItem(Name: name)
            : new CardholderGroupBatchItem(Id: existingId);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(action, Items: [firstItem, null!])));
        Assert.Equal("items", error.ParamName);
        Assert.Contains("index 1", error.Message);

        if (existingId.HasValue)
            await IntegrationTestHelpers.AssertEntityAsync(sc, existingId.Value, "CardholderGroup", name);
        else
        {
            var matches = await sc.Manager.GetClient().SearchEntities(
                name: name, entityType: "CardholderGroup", nameSearchMode: "Is", pageSize: 1);
            var ids = IntegrationTestHelpers.ExtractGuids(matches);
            foreach (var id in ids) sc.TrackForCleanup(id);
            Assert.Empty(ids);
        }
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.List));

        Assert.IsType<string>(result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Create_BatchMode_ReturnsAllGuids(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var names = Enumerable.Range(1, count)
            .Select(i => $"MCP_Group_{i}_{Guid.NewGuid():N}")
            .ToList();

        var result = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Create,
                Items: names.Select(name => new CardholderGroupBatchItem(Name: name)).ToList()));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.RequestedCount);
        Assert.Equal(count, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });
    }

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Group_{Guid.NewGuid():N}";

        // Create
        var createResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Create,
                Items: [new CardholderGroupBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        // Get
        var getResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        // Update
        var newName = $"{name}_Updated";
        var updateResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Update,
                Id: created.Guid, Body: new CardholderGroupUpdateProperties(Name: newName)));
        Assert.IsType<string>(updateResult);

        // Delete
        var deleteResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Delete, Id: created.Guid));
        Assert.NotNull(deleteResult);
    }

    [Fact]
    public async Task GetMembers_AddMember_RemoveMember_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        // Create a group
        var groupName = $"MCP_Group_{Guid.NewGuid():N}";
        var groupCreateResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Create,
                Items: [new CardholderGroupBatchItem(Name: groupName)]));
        var group = Assert.IsType<EntityCreateResult>(groupCreateResult);
        sc.TrackForCleanup(group.Guid);

        // Create a cardholder to use as member
        var chName = $"MCP_Cardholder_{Guid.NewGuid():N}";
        var chCreateResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(chName)]));
        var ch = Assert.IsType<EntityCreateResult>(chCreateResult);
        sc.TrackForCleanup(ch.Guid);

        // GetMembers — empty initially
        var membersResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.GetMembers, Id: group.Guid));
        Assert.IsType<string>(membersResult);

        // AddMember
        var addResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: group.Guid, MemberIds: [ch.Guid]));
        Assert.IsType<string>(addResult);

        // RemoveMember
        var removeResult = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.RemoveMember,
                Id: group.Guid, MemberIds: [ch.Guid]));
        Assert.IsType<string>(removeResult);
    }

    [Fact]
    public async Task TypeGuards_RejectWrongGroupAndMembers_PreserveMixedBatchResults()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);

        foreach (var action in new[]
                 {
                     CardholderGroupAction.Get,
                     CardholderGroupAction.Update,
                     CardholderGroupAction.Delete,
                     CardholderGroupAction.GetMembers,
                     CardholderGroupAction.AddMember,
                     CardholderGroupAction.RemoveMember
                 })
        {
            var request = action switch
            {
                CardholderGroupAction.Update => new CardholderGroupManagementRequest(action, Id: wrongTarget.Id,
                    Body: new CardholderGroupUpdateProperties(Name: "Must not update")),
                CardholderGroupAction.AddMember or CardholderGroupAction.RemoveMember =>
                    new CardholderGroupManagementRequest(action, Id: wrongTarget.Id, MemberIds: [Guid.NewGuid()]),
                _ => new CardholderGroupManagementRequest(action, Id: wrongTarget.Id)
            };
            var error = await Assert.ThrowsAsync<ArgumentException>(() =>
                CardholderGroupManagementTool.Execute(sc.Manager, request));
            Assert.Equal(action == CardholderGroupAction.Delete ? "items" : "id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var groupName = $"MCP_Group_TypeGuard_{Guid.NewGuid():N}";
        var group = Assert.IsType<EntityCreateResult>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Create,
                Items: [new CardholderGroupBatchItem(Name: groupName)])));
        sc.TrackForCleanup(group.Guid);
        var cardholder = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Cardholder_TypeGuard_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(cardholder.Guid);

        var initialMembers = Assert.IsType<string>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.GetMembers, Id: group.Guid)));
        foreach (var action in new[] { CardholderGroupAction.AddMember, CardholderGroupAction.RemoveMember })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGroupManagementTool.Execute(sc.Manager,
                new CardholderGroupManagementRequest(action, Id: group.Guid, MemberIds: [wrongTarget.Id])));
            Assert.Equal("member_ids", error.ParamName);
            var members = Assert.IsType<string>(await CardholderGroupManagementTool.Execute(sc.Manager,
                new CardholderGroupManagementRequest(CardholderGroupAction.GetMembers, Id: group.Guid)));
            Assert.Equal(initialMembers, members);
        }

        var add = Assert.IsType<BatchCreateResult>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.AddMember,
                Id: group.Guid, MemberIds: [cardholder.Guid, wrongTarget.Id])));
        Assert.Equal((2, 1, 1), (add.RequestedCount, add.SuccessCount, add.FailureCount));
        var afterAdd = Assert.IsType<string>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.GetMembers, Id: group.Guid)));
        Assert.Contains(cardholder.Guid.ToString(), afterAdd, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(wrongTarget.Id.ToString(), afterAdd, StringComparison.OrdinalIgnoreCase);

        var remove = Assert.IsType<BatchCreateResult>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.RemoveMember,
                Id: group.Guid, MemberIds: [cardholder.Guid, wrongTarget.Id])));
        Assert.Equal((2, 1, 1), (remove.RequestedCount, remove.SuccessCount, remove.FailureCount));
        var afterRemove = Assert.IsType<string>(await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.GetMembers, Id: group.Guid)));
        Assert.DoesNotContain(cardholder.Guid.ToString(), afterRemove, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(wrongTarget.Id.ToString(), afterRemove, StringComparison.OrdinalIgnoreCase);

        var deleteBatch = await CardholderGroupManagementTool.Execute(sc.Manager,
            new CardholderGroupManagementRequest(CardholderGroupAction.Delete,
                Items: [new CardholderGroupBatchItem(Id: group.Guid), new CardholderGroupBatchItem(Id: wrongTarget.Id)]));
        IntegrationTestHelpers.AssertPartialBatchResult(deleteBatch, group.Guid, wrongTarget.Id);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, group.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
