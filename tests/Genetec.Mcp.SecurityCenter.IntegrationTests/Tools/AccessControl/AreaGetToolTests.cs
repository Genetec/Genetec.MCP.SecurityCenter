using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingArea_ReturnsEntityContainingName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaGetTool.Execute(sc.Manager, new AreaGetTool.AreaGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaGetTool.Execute(sc.Manager,
            new AreaGetTool.AreaGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
