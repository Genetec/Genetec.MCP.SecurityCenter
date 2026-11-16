using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task SingleCreate_ReturnsValidGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_Alarm_{Guid.NewGuid():N}";
        var result = await AlarmCreateTool.Execute(sc.Manager,
            new AlarmCreateRequest(Items: [new AlarmItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task BatchCreate_ReturnsExpectedCounts()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        const int count = 3;
        var items = Enumerable.Range(1, count)
            .Select(i => new AlarmItem(Name: $"MCP_AlarmBatch_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await AlarmCreateTool.Execute(sc.Manager, new AlarmCreateRequest(Items: items));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.RequestedCount);
        Assert.Equal(count, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });
    }
}
