using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsNonNullResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await AlarmListTool.Execute(sc.Manager, new AlarmListRequest());
        Assert.NotNull(result);
    }
}
