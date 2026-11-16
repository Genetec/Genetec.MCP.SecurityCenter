using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class SystemConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsSystemConfig()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await SystemConfigurationTool.Execute(sc.Manager);

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}
