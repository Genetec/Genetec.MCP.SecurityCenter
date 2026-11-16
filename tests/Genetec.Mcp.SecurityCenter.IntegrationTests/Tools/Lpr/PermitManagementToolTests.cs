using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class PermitManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(PermitAction.Create)]
    [InlineData(PermitAction.Delete)]
    public async Task Batch_NullItem_RejectsBeforeMutation(PermitAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_NullBatch_{Guid.NewGuid():N}";
        Guid? existingId = null;
        if (action == PermitAction.Delete)
        {
            var created = Assert.IsType<EntityCreateResult>(await PermitManagementTool.Execute(sc.Manager,
                new PermitManagementRequest(PermitAction.Create, Name: name)));
            existingId = created.Guid;
            sc.TrackForCleanup(created.Guid);
        }

        var firstItem = action == PermitAction.Create
            ? new PermitBatchItem(Name: name)
            : new PermitBatchItem(Id: existingId);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(action, Items: [firstItem, null!])));
        Assert.Equal("items", error.ParamName);
        Assert.Contains("index 1", error.Message);

        if (existingId.HasValue)
            await IntegrationTestHelpers.AssertEntityAsync(sc, existingId.Value, "Permit", name);
        else
        {
            var matches = await sc.Manager.GetClient().SearchEntities(
                name: name, entityType: "Permit", nameSearchMode: "Is", pageSize: 1);
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

        var name = $"MCP_Permit_{Guid.NewGuid():N}";

        var createResult = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Create, Name: name));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var getResult = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        var updateResult = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Update, Id: created.Guid, Name: $"{name}_Updated"));
        Assert.IsType<string>(updateResult);

        var deleteResult = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Delete, Id: created.Guid));
        Assert.NotNull(deleteResult);
    }

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task GetUpdateDelete_UserGroupGuidAndMixedBatch_PreserveUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var requests = new[]
        {
            new PermitManagementRequest(PermitAction.Get, Id: wrongTarget.Id),
            new PermitManagementRequest(PermitAction.Update, Id: wrongTarget.Id,
                Body: new PermitUpdateProperties(Name: "Must not update")),
            new PermitManagementRequest(PermitAction.Delete, Id: wrongTarget.Id)
        };

        foreach (var request in requests)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitManagementTool.Execute(sc.Manager, request));
            Assert.Equal(request.Action == PermitAction.Delete ? "items" : "id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var created = Assert.IsType<EntityCreateResult>(await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Create, Name: $"MCP_TypeGuard_{Guid.NewGuid():N}")));
        sc.TrackForCleanup(created.Guid);
        var batch = await PermitManagementTool.Execute(sc.Manager,
            new PermitManagementRequest(PermitAction.Delete,
                Items: [new PermitBatchItem(Id: created.Guid), new PermitBatchItem(Id: wrongTarget.Id)]));

        IntegrationTestHelpers.AssertPartialBatchResult(batch, created.Guid, wrongTarget.Id);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
