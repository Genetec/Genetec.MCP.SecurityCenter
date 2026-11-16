using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class ParkingZoneToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_ParkingZone_{Guid.NewGuid():N}";

        var createResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "ParkingZone", name);

        var getResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        var updateResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Update, Items: [new ParkingZoneBatchItem(Name: $"{name}_Updated", Id: created.Guid)]));
        Assert.IsType<string>(updateResult);

        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "ParkingZone", $"{name}_Updated");

        var deleteResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items: [new ParkingZoneBatchItem(Id: created.Guid)]));
        Assert.IsType<StatusResult>(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.List));

        Assert.IsType<string>(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TypedItems_CreateReadUpdateDeleteReadback_Succeeds(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var names = Enumerable.Range(0, count).Select(_ => $"MCP_ParkingZone_Typed_{Guid.NewGuid():N}").ToList();
        var createResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: names.Select(name => new ParkingZoneBatchItem(Name: name)).ToList()));
        List<Guid> ids;

        if (count == 1)
        {
            var created = Assert.IsType<EntityCreateResult>(createResult);
            sc.TrackForCleanup(created.Guid);
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
            var result = await ParkingZoneTool.Execute(sc.Manager, new ParkingZoneManagementRequest(ParkingZoneAction.Get, Id: ids[index]));
            Assert.IsType<string>(result);
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "ParkingZone", names[index]);
        }
        var updateResult = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Update, Items: ids.Select((id, index) =>
                new ParkingZoneBatchItem(Id: id, Body: new ParkingZoneUpdateProperties(Name: names[index] + "_Updated"))).ToList()));
        if (count > 1)
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(updateResult));
            var batch = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(count, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(0, batch.GetProperty("failure_count").GetInt32());
        }
        for (var index = 0; index < ids.Count; index++)
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "ParkingZone", names[index] + "_Updated");
        var deleted = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items: ids.Select(id => new ParkingZoneBatchItem(Id: id)).ToList()));
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
    [InlineData(ParkingZoneAction.Get)]
    [InlineData(ParkingZoneAction.Update)]
    [InlineData(ParkingZoneAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(ParkingZoneAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action == ParkingZoneAction.Get ? new ParkingZoneManagementRequest(action, Id: id) : new ParkingZoneManagementRequest(action, Items: [new ParkingZoneBatchItem(Id: id, Body: action == ParkingZoneAction.Update ? new ParkingZoneUpdateProperties(Name: "Must not update") : null)]));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(sc.Manager, request));
        Assert.Equal(action == ParkingZoneAction.Get ? "id" : "items", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData("Hotlist", "items.body.hotlist")]
    [InlineData("LprManager", "items.body.lpr_manager")]
    public async Task Update_WrongLinkedEntityType_RejectsBeforeMutation(string property, string parameter)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_LinkType_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var body = property switch
        {
            "Hotlist" => new ParkingZoneUpdateProperties(Hotlist: wrongId),
            "LprManager" => new ParkingZoneUpdateProperties(LprManager: wrongId),

            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Update, Items: [new ParkingZoneBatchItem(Id: id, Body: body)])));
        Assert.Equal(parameter, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "ParkingZone", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_WrongEntityTypeInItems_RejectsSingleAndBatch(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items: Enumerable.Range(0, count).Select(_ => new ParkingZoneBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await ParkingZoneTool.Execute(sc.Manager, request)));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData("Hotlist")]
    [InlineData("LprManager")]
    public async Task BatchUpdate_WrongLinkedType_PreservesValidUpdate(string property)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var names = new[] { $"MCP_ParkingZone_Valid_{Guid.NewGuid():N}", $"MCP_ParkingZone_Rejected_{Guid.NewGuid():N}" };
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var created = Assert.IsType<EntityCreateResult>(await ParkingZoneTool.Execute(sc.Manager,
                new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: name)])));
            sc.TrackForCleanup(created.Guid);
            ids.Add(created.Guid);
        }
        var invalidBody = property switch
        {
            "Hotlist" => new ParkingZoneUpdateProperties(Hotlist: wrongId),
            "LprManager" => new ParkingZoneUpdateProperties(LprManager: wrongId),
            _ => throw new InvalidOperationException()
        };
        var result = await ParkingZoneTool.Execute(sc.Manager, new ParkingZoneManagementRequest(ParkingZoneAction.Update, Items:
        [
            new ParkingZoneBatchItem(Id: ids[0], Body: new ParkingZoneUpdateProperties(Name: names[0] + "_Updated")),
            new ParkingZoneBatchItem(Id: ids[1], Body: invalidBody)
        ]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, ids[0], ids[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[0], "ParkingZone", names[0] + "_Updated");
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[1], "ParkingZone", names[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Fact]
    public async Task BatchDelete_WrongType_PreservesValidDelete()
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var created = Assert.IsType<EntityCreateResult>(await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: $"MCP_ParkingZone_Delete_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var result = await ParkingZoneTool.Execute(sc.Manager, new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items:
            [new ParkingZoneBatchItem(Id: created.Guid), new ParkingZoneBatchItem(Id: wrongId)]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, created.Guid, wrongId);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }
}
