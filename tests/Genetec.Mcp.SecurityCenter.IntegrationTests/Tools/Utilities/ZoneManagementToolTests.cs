using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class ZoneManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.List));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Zone_{Guid.NewGuid():N}";

        var createResult = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "Zone", name);

        var getResult = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);

        var newName = $"{name}_Updated";
        var updateResult = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Update, Items: [new ZoneBatchItem(Id: created.Guid, Body: new ZoneUpdateProperties(Name: newName))]));
        Assert.IsType<string>(updateResult);

        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "Zone", newName);

        await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Delete, Items: [new ZoneBatchItem(Id: created.Guid)]));
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task BatchCreate_MultipleZones_ReturnsBatchResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var names = new List<string>
        {
            $"MCP_Zone_{Guid.NewGuid():N}",
            $"MCP_Zone_{Guid.NewGuid():N}"
        };

        var result = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: names.Select(name => new ZoneBatchItem(Name: name)).ToList()));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(2, batch.SuccessCount);

        foreach (var item in batch.Results)
        {
            if (item.Guid is not null)
                sc.TrackForCleanup(item.Guid);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TypedItems_CreateReadUpdateDeleteReadback_Succeeds(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var names = Enumerable.Range(0, count).Select(_ => $"MCP_Zone_Typed_{Guid.NewGuid():N}").ToList();
        var createResult = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: names.Select(name => new ZoneBatchItem(Name: name)).ToList()));
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
            var result = await ZoneManagementTool.Execute(sc.Manager, new ZoneManagementRequest(ZoneAction.Get, Id: ids[index]));
            Assert.IsType<string>(result);
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "Zone", names[index]);
        }
        var updateResult = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Update, Items: ids.Select((id, index) =>
                new ZoneBatchItem(Id: id, Body: new ZoneUpdateProperties(Name: names[index] + "_Updated"))).ToList()));
        if (count > 1)
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(updateResult));
            var batch = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(count, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(0, batch.GetProperty("failure_count").GetInt32());
        }
        for (var index = 0; index < ids.Count; index++)
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "Zone", names[index] + "_Updated");
        var deleted = await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Delete, Items: ids.Select(id => new ZoneBatchItem(Id: id)).ToList()));
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
    [InlineData(ZoneAction.Get)]
    [InlineData(ZoneAction.Update)]
    [InlineData(ZoneAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(ZoneAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action == ZoneAction.Get ? new ZoneManagementRequest(action, Id: id) : new ZoneManagementRequest(action, Items: [new ZoneBatchItem(Id: id, Body: action == ZoneAction.Update ? new ZoneUpdateProperties(Name: "Must not update") : null)]));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action == ZoneAction.Get ? "id" : "items", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData(ZoneAction.Create)]
    [InlineData(ZoneAction.Update)]
    public async Task AccessControlUnit_ValidUnit_PassesGuardBeforeRejectingInvalidInterfaceModule(ZoneAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        var listed = await client.ListEntities("Unit", 1, 1);
        var selectedId = ScIntegrationFixture.ExtractFirstGuid(listed);
        Assert.True(selectedId.HasValue, "Unit validation requires an existing access control unit.");
        var unitId = selectedId.Value;
        var name = $"MCP_Zone_UnitGuard_{Guid.NewGuid():N}";
        Guid? zoneId = null;
        if (action == ZoneAction.Update)
        {
            var created = Assert.IsType<EntityCreateResult>(await ZoneManagementTool.Execute(sc.Manager,
                new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: name)])));
            zoneId = created.Guid;
            sc.TrackForCleanup(zoneId);
        }

        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, unitId, "Unit", wrong => ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(action, Items: [new ZoneBatchItem(Name: action == ZoneAction.Create ? name : null, Id: zoneId, Body: new ZoneUpdateProperties(AccessControlUnit: unitId, InterfaceModule: wrong))])), "items.body.interface_module");

        if (zoneId is { } id)
            await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Zone", name);
    }

    [Theory]
    [InlineData("AccessControlUnit", "items.body.access_control_unit")]
    [InlineData("InterfaceModule", "items.body.interface_module")]
    [InlineData("Owner", "items.body.owner")]
    public async Task Update_WrongLinkedEntityType_RejectsBeforeMutation(string property, string parameter)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_LinkType_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var body = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),

            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Update, Items: [new ZoneBatchItem(Id: id, Body: body)])));
        Assert.Equal(parameter, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Zone", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Theory]
    [InlineData("AccessControlUnit", "items.body.access_control_unit")]
    [InlineData("InterfaceModule", "items.body.interface_module")]
    [InlineData("Owner", "items.body.owner")]
    public async Task Create_WrongLinkedEntityType_RejectsBeforeCreatingZone(string property, string parameter)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var body = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: "Must not create", Body: body)])));
        Assert.Equal(parameter, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_WrongEntityTypeInItems_RejectsSingleAndBatch(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new ZoneManagementRequest(ZoneAction.Delete, Items: Enumerable.Range(0, count).Select(_ => new ZoneBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await ZoneManagementTool.Execute(sc.Manager, request)));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData("AccessControlUnit")]
    [InlineData("InterfaceModule")]
    [InlineData("Owner")]
    public async Task BatchUpdate_WrongLinkedType_PreservesValidUpdate(string property)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var names = new[] { $"MCP_Zone_Valid_{Guid.NewGuid():N}", $"MCP_Zone_Rejected_{Guid.NewGuid():N}" };
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var created = Assert.IsType<EntityCreateResult>(await ZoneManagementTool.Execute(sc.Manager,
                new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: name)])));
            sc.TrackForCleanup(created.Guid);
            ids.Add(created.Guid);
        }
        var invalidBody = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),
            _ => throw new InvalidOperationException()
        };
        var result = await ZoneManagementTool.Execute(sc.Manager, new ZoneManagementRequest(ZoneAction.Update, Items:
        [
            new ZoneBatchItem(Id: ids[0], Body: new ZoneUpdateProperties(Name: names[0] + "_Updated")),
            new ZoneBatchItem(Id: ids[1], Body: invalidBody)
        ]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, ids[0], ids[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[0], "Zone", names[0] + "_Updated");
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[1], "Zone", names[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Fact]
    public async Task BatchDelete_WrongType_PreservesValidDelete()
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var created = Assert.IsType<EntityCreateResult>(await ZoneManagementTool.Execute(sc.Manager,
            new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: $"MCP_Zone_Delete_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var result = await ZoneManagementTool.Execute(sc.Manager, new ZoneManagementRequest(ZoneAction.Delete, Items:
            [new ZoneBatchItem(Id: created.Guid), new ZoneBatchItem(Id: wrongId)]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, created.Guid, wrongId);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Fact]
    public async Task BatchCreate_WrongLinkedType_PreservesValidCreate()
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_Zone_ValidBody_{Guid.NewGuid():N}";
        var result = await ZoneManagementTool.Execute(sc.Manager, new ZoneManagementRequest(ZoneAction.Create, Items:
        [
            new ZoneBatchItem(Name: name, Body: new ZoneUpdateProperties()),
            new ZoneBatchItem(Name: "Must not create", Body: new ZoneUpdateProperties(AccessControlUnit: wrongId))
        ]));
        var batch = Assert.IsType<BatchCreateResult>(result);
        foreach (var item in batch.Results)
            if (item.Guid is { } id) sc.TrackForCleanup(id);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        Assert.True(batch.Results[0].Success);
        var createdId = Assert.IsType<Guid>(batch.Results[0].Guid);
        Assert.False(batch.Results[1].Success);
        Assert.Contains("items.body.access_control_unit", batch.Results[1].Error);
        await IntegrationTestHelpers.AssertEntityAsync(sc, createdId, "Zone", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }
}
