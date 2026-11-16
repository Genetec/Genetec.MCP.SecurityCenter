using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class ActivityTrailToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Query_ValidTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ActivityTrailTool.Execute(
            sc.Manager,
            new ActivityTrailRequest("2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z"));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task Query_WithInstigatorId_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ActivityTrailTool.Execute(
            sc.Manager,
            new ActivityTrailRequest("2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z",
                InstigatorId: new Guid("00000000-0000-0000-0000-000000000001")));

        Assert.NotNull(result);
    }
}
