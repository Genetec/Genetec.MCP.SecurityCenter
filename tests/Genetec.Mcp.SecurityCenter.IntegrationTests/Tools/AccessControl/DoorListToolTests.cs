using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await DoorListTool.Execute(sc.Manager, new DoorListRequest());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task List_WithPagination_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await DoorListTool.Execute(sc.Manager, new DoorListRequest(PageSize: 10, Page: 1));

        Assert.NotNull(result);
    }
}
