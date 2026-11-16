using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await VisitorListTool.Execute(sc.Manager, new VisitorListRequest());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task List_WithPageSize_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await VisitorListTool.Execute(sc.Manager, new VisitorListRequest(PageSize: 10, Page: 1));

        Assert.NotNull(result);
    }
}
