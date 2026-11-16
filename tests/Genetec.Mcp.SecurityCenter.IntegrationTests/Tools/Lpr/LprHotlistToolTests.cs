using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class LprHotlistToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(LprHotlistAction.Create)]
    [InlineData(LprHotlistAction.Delete)]
    public async Task Batch_NullItem_RejectsBeforeMutation(LprHotlistAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_NullBatch_{Guid.NewGuid():N}";
        Guid? existingId = null;
        if (action == LprHotlistAction.Delete)
        {
            var created = Assert.IsType<EntityCreateResult>(await LprHotlistTool.Execute(sc.Manager,
                new LprHotlistManagementRequest(LprHotlistAction.Create, Name: name)));
            existingId = created.Guid;
            sc.TrackForCleanup(created.Guid);
        }

        var firstItem = action == LprHotlistAction.Create
            ? new LprHotlistBatchItem(Name: name)
            : new LprHotlistBatchItem(Id: existingId);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(action, Items: [firstItem, null!])));
        Assert.Equal("items", error.ParamName);
        Assert.Contains("index 1", error.Message);

        if (existingId.HasValue)
            await IntegrationTestHelpers.AssertEntityAsync(sc, existingId.Value, "HotlistRule", name);
        else
        {
            var matches = await sc.Manager.GetClient().SearchEntities(
                name: name, entityType: "HotlistRule", nameSearchMode: "Is", pageSize: 1);
            var ids = IntegrationTestHelpers.ExtractGuids(matches);
            foreach (var id in ids) sc.TrackForCleanup(id);
            Assert.Empty(ids);
        }
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Hotlist_{Guid.NewGuid():N}";

        var createResult = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Create, Name: name));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var getResult = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        var newName = $"{name}_Updated";
        var updateResult = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Update, Id: created.Guid, Name: newName));
        Assert.IsType<string>(updateResult);

        var deleteResult = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Delete, Id: created.Guid));
        Assert.NotNull(deleteResult);
    }

    [Fact]
    public async Task Create_BatchMode_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var count = 2;
        var names = Enumerable.Range(1, count).Select(i => $"MCP_Hotlist_{i}_{Guid.NewGuid():N}").ToList();

        var result = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Create,
                Items: names.Select(name => new LprHotlistBatchItem(Name: name)).ToList()));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.SuccessCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });
    }

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task GetUpdateDelete_UserGroupGuidAndMixedBatch_PreserveUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var requests = new[]
        {
            new LprHotlistManagementRequest(LprHotlistAction.Get, Id: wrongTarget.Id),
            new LprHotlistManagementRequest(LprHotlistAction.Update, Id: wrongTarget.Id,
                Update: new LprHotlistUpdateRequest(Name: "Must not update")),
            new LprHotlistManagementRequest(LprHotlistAction.Delete, Id: wrongTarget.Id)
        };

        foreach (var request in requests)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => LprHotlistTool.Execute(sc.Manager, request));
            Assert.Equal(request.Action == LprHotlistAction.Delete ? "items" : "id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var created = Assert.IsType<EntityCreateResult>(await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Create, Name: $"MCP_TypeGuard_{Guid.NewGuid():N}")));
        sc.TrackForCleanup(created.Guid);
        var batch = await LprHotlistTool.Execute(sc.Manager,
            new LprHotlistManagementRequest(LprHotlistAction.Delete,
                Items: [new LprHotlistBatchItem(Id: created.Guid), new LprHotlistBatchItem(Id: wrongTarget.Id)]));

        IntegrationTestHelpers.AssertPartialBatchResult(batch, created.Guid, wrongTarget.Id);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
