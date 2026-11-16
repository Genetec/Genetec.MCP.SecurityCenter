using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CardholderListTool.Execute(sc.Manager, new CardholderListRequest());

        Assert.NotNull(result);
    }
}
