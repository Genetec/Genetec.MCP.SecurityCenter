using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class IntrusionUnitManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.List));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Get_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No IntrusionUnit is configured in Security Center. This test requires an enrolled intrusion unit.");
        var result = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.Get, Id: id));
        Assert.IsType<string>(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetArea_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No IntrusionUnit is configured in Security Center. This test requires an enrolled intrusion unit.");
        var result = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.GetArea, Id: id));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        var areas = IntegrationTestHelpers.GetResult(document.RootElement).GetProperty("IntrusionAreas");
        Assert.Equal(JsonValueKind.Array, areas.ValueKind);

        using var unit = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(id.Value.ToString()));
        var unitResult = IntegrationTestHelpers.GetResult(unit.RootElement);
        Assert.Equal(id.Value, unitResult.GetProperty("Guid").GetGuid());
        Assert.Equal(unitResult.GetProperty("IntrusionAreas").EnumerateArray().Select(area => area.GetGuid()),
            areas.EnumerateArray().Select(area => area.GetGuid()));
        foreach (var area in areas.EnumerateArray())
        {
            using var entity = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(area.GetGuid().ToString()));
            var areaResult = IntegrationTestHelpers.GetResult(entity.RootElement);
            Assert.Equal(area.GetGuid(), areaResult.GetProperty("Guid").GetGuid());
            Assert.Equal("IntrusionArea", areaResult.GetProperty("EntityType").GetString());
        }
    }

    [Fact]
    public async Task GetUpdate_ExistingTarget_ReadsBackAndRestoresName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var targets = await sc.Manager.GetClient().ListEntities("IntrusionUnit", 50, 1);
        using var document = IntegrationTestHelpers.ParseJson(targets);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, rows.ValueKind);
        Assert.True(rows.GetArrayLength() > 0, "This test requires a configured IntrusionUnit.");
        var id = rows[0].GetProperty("Guid").GetGuid();
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, id, "IntrusionUnit");
        var result = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.Get, Id: id));
        using var read = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(id, IntegrationTestHelpers.GetResult(read.RootElement).GetProperty("Guid").GetGuid());
        var updatedName = $"MCP_IntrusionUnit_{Guid.NewGuid():N}";
        try
        {
            await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.Update, Items: [new IntrusionUnitManagementItem(Id: id, Body: new IntrusionUnitUpdateProperties(Name: updatedName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, id, "IntrusionUnit", updatedName);
        }
        finally
        {
            await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.Update, Items: [new IntrusionUnitManagementItem(Id: id, Body: new IntrusionUnitUpdateProperties(Name: originalName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, id, "IntrusionUnit", originalName);
        }
    }


    [Fact]
    public async Task Delete_UnknownId_ReturnsSdkEntityNotFound()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = Guid.NewGuid();
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, id);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => IntrusionUnitManagementTool.Execute(sc.Manager,
            new IntrusionUnitManagementRequest(IntrusionUnitAction.Delete, Items: [new IntrusionUnitManagementItem(Id: id)])));
        Assert.Contains("\"SdkErrorCode\":\"UnableToRetrieveEntity\"", error.Message, StringComparison.Ordinal);
        Assert.Contains(id.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, id);
    }

    [Fact]
    public async Task Move_UnknownIds_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => IntrusionUnitManagementTool.Execute(sc.Manager,
            new IntrusionUnitManagementRequest(IntrusionUnitAction.Move,
                Id: Guid.NewGuid(), TargetUnitId: Guid.NewGuid())));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Theory]
    [InlineData(IntrusionUnitAction.Get)]
    [InlineData(IntrusionUnitAction.Update)]
    [InlineData(IntrusionUnitAction.Delete)]
    [InlineData(IntrusionUnitAction.GetArea)]
    [InlineData(IntrusionUnitAction.Move)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(IntrusionUnitAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action is IntrusionUnitAction.Update or IntrusionUnitAction.Delete ? new IntrusionUnitManagementRequest(action, Items: [new IntrusionUnitManagementItem(Id: id, Body: action == IntrusionUnitAction.Update ? new IntrusionUnitUpdateProperties(Name: "Must not update") : null)]) : new IntrusionUnitManagementRequest(action, Id: id, TargetUnitId: Guid.NewGuid()));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionUnitManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action is IntrusionUnitAction.Update or IntrusionUnitAction.Delete ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Fact]
    public async Task Move_WrongDestinationType_RejectsBeforeAction()
    {
        var (targetId, targetName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var list = await IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.List));
        var unitId = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(unitId.HasValue, "This test requires an enrolled intrusion unit.");
        var id = unitId.Value;
        var name = await IntegrationTestHelpers.GetEntityNameAsync(sc, id, "IntrusionUnit");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionUnitManagementTool.Execute(sc.Manager,
            new IntrusionUnitManagementRequest(IntrusionUnitAction.Move, Id: id, TargetUnitId: targetId)));
        Assert.Equal("target_unit_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "IntrusionUnit", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, targetId, "UserGroup", targetName);
    }
}
