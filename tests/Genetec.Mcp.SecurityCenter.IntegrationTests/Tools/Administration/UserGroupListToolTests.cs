using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsNonNullResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await UserGroupListTool.Execute(sc.Manager, new UserGroupListRequest());
        Assert.NotNull(result);
    }
}
