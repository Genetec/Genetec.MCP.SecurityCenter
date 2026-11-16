using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class MacroManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetListUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Macro_{Guid.NewGuid():N}";

        // Create
        var createResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Create, Items: [new MacroManagementItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        // Get
        var getResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        // List
        var listResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.List));
        Assert.NotNull(listResult);

        // Update
        var updatedName = $"MCP_Macro_Updated_{Guid.NewGuid():N}";
        var updateResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Update, Items: [new MacroManagementItem(Name: updatedName, Id: created.Guid)]));
        Assert.IsType<string>(updateResult);

        // Delete
        var deleteResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Delete, Items: [new MacroManagementItem(Id: created.Guid)]));
        Assert.NotNull(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task Execute_MacroWithNoScript_DoesNotThrow()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Macro_Exec_{Guid.NewGuid():N}";

        var createResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Create, Items: [new MacroManagementItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        // Executing a macro with no script may return an error response — that is acceptable.
        // We only assert that the call completes without throwing an unhandled exception.
        var execResult = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Execute, Id: created.Guid));
        Assert.NotNull(execResult);
    }

    [Fact]
    public async Task Create_BatchMode_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        const int count = 2;
        var names = Enumerable.Range(1, count)
            .Select(i => $"MCP_Macro_Batch_{i}_{Guid.NewGuid():N}")
            .ToList();

        var result = await MacroManagementTool.Execute(
            sc.Manager, new MacroManagementRequest(MacroAction.Create, Items: names.Select(name => new MacroManagementItem(Name: name)).ToList()));

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
    [InlineData(MacroAction.Get)]
    [InlineData(MacroAction.Update)]
    [InlineData(MacroAction.Delete)]
    [InlineData(MacroAction.Execute)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(MacroAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action is MacroAction.Update or MacroAction.Delete ? new MacroManagementRequest(action, Items: [new MacroManagementItem(Id: id, Body: action == MacroAction.Update ? new MacroUpdateProperties(Name: "Must not update") : null)]) : new MacroManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MacroManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action is MacroAction.Update or MacroAction.Delete ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
