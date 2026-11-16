using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class ScheduleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetUpdateListDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Sched_{Guid.NewGuid():N}";

        // Create
        var createResult = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.Create, Items: [new ScheduleBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "Schedule", name);

        // Get
        var getResult = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        // Update
        var updatedName = $"MCP_Sched_Updated_{Guid.NewGuid():N}";
        var updateResult = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.Update, Items: [new ScheduleBatchItem(Name: updatedName, Id: created.Guid)]));
        Assert.IsType<string>(updateResult);

        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "Schedule", updatedName);

        // List
        var listResult = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.List));
        Assert.NotNull(listResult);

        // Delete
        var deleteResult = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.Delete, Items: [new ScheduleBatchItem(Id: created.Guid)]));
        Assert.IsType<StatusResult>(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task Create_BatchMode_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        const int count = 2;
        var names = Enumerable.Range(1, count)
            .Select(i => $"MCP_Sched_Batch_{i}_{Guid.NewGuid():N}")
            .ToList();

        var result = await ScheduleManagementTool.Execute(
            sc.Manager, new ScheduleManagementRequest(ScheduleAction.Create, Items: names.Select(name => new ScheduleBatchItem(Name: name)).ToList()));

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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TypedItems_CreateReadUpdateDeleteReadback_Succeeds(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var names = Enumerable.Range(0, count).Select(_ => $"MCP_Schedule_Typed_{Guid.NewGuid():N}").ToList();
        var createResult = await ScheduleManagementTool.Execute(sc.Manager,
            new ScheduleManagementRequest(ScheduleAction.Create, Items: names.Select(name => new ScheduleBatchItem(Name: name)).ToList()));
        List<Guid> ids;
        if (count == 1)
        {
            var created = Assert.IsType<EntityCreateResult>(createResult);
            sc.TrackForCleanup(created.Guid);
            Assert.NotEqual(Guid.Empty, created.Guid);
            ids = [created.Guid];
        }
        else
        {
            var batch = Assert.IsType<BatchCreateResult>(createResult);
            foreach (var result in batch.Results)
                if (result.Guid is { } id) sc.TrackForCleanup(id);
            Assert.Equal(count, batch.RequestedCount);
            Assert.Equal(count, batch.SuccessCount);
            Assert.Equal(0, batch.FailureCount);
            ids = batch.Results.Select(result => Assert.IsType<Guid>(result.Guid)).ToList();
        }
        for (var index = 0; index < ids.Count; index++)
        {
            var result = await ScheduleManagementTool.Execute(sc.Manager, new ScheduleManagementRequest(ScheduleAction.Get, Id: ids[index]));
            Assert.IsType<string>(result);
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "Schedule", names[index]);
            var updatedName = names[index] + "_Updated";
            await ScheduleManagementTool.Execute(sc.Manager,
                new ScheduleManagementRequest(ScheduleAction.Update, Items: [new ScheduleBatchItem(Id: ids[index], Body: new ScheduleUpdateProperties(Name: updatedName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "Schedule", updatedName);
        }
        var deleted = await ScheduleManagementTool.Execute(sc.Manager,
            new ScheduleManagementRequest(ScheduleAction.Delete, Items: ids.Select(id => new ScheduleBatchItem(Id: id)).ToList()));
        if (count == 1) Assert.IsType<StatusResult>(deleted);
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(deleted));
            var batch = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(count, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(0, batch.GetProperty("failure_count").GetInt32());
        }
        foreach (var id in ids) await IntegrationTestHelpers.AssertEntityMissingAsync(sc, id);
    }

    [Theory]
    [InlineData(ScheduleAction.Get)]
    [InlineData(ScheduleAction.Update)]
    [InlineData(ScheduleAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(ScheduleAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action is ScheduleAction.Update or ScheduleAction.Delete ? new ScheduleManagementRequest(action, Items: [new ScheduleBatchItem(Id: id, Body: action == ScheduleAction.Update ? new ScheduleUpdateProperties(Name: "Must not update") : null)]) : new ScheduleManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action is ScheduleAction.Update or ScheduleAction.Delete ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_WrongEntityTypeInItems_RejectsSingleAndBatch(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new ScheduleManagementRequest(ScheduleAction.Delete,
            Items: Enumerable.Range(0, count).Select(_ => new ScheduleBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(await ScheduleManagementTool.Execute(sc.Manager, request)));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
