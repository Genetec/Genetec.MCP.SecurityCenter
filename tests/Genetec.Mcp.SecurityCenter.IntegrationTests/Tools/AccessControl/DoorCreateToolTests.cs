using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_Single_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Door_{Guid.NewGuid():N}";
        var result = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Create_Batch_ReturnsBatchCreateResult(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = Enumerable.Range(1, count)
            .Select(i => new DoorItem($"MCP_Door_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await DoorCreateTool.Execute(sc.Manager, new DoorCreateRequest(items));
        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.RequestedCount);
        Assert.Equal(count, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        foreach (var r in batch.Results)
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        }
    }
}
