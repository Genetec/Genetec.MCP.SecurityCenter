using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class RoleListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsNonNullResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await RoleListTool.Execute(sc.Manager, new RoleListRequest());
        Assert.NotNull(result);
    }
}
