using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsNonNullResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await UserListTool.Execute(sc.Manager, new UserListRequest());
        Assert.NotNull(result);
    }
}
