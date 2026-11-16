using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class NetworkManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.List));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task Get_ValidId_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.NotNull(id);
        var result = await NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.Get, Id: id));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetEndpoints_ValidId_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.NotNull(id);
        var result = await NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.GetEndpoints, Id: id));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(NetworkAction.Get)]
    [InlineData(NetworkAction.GetEndpoints)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(NetworkAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new NetworkManagementRequest(action, Id: id);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => NetworkManagementTool.Execute(sc.Manager, request));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
