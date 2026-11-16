using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_Single_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Visitor_{Guid.NewGuid():N}";
        var result = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem(name)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Create_Batch_ReturnsBatchCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = Enumerable.Range(1, 3)
            .Select(i => new VisitorItem($"MCP_Visitor_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await VisitorCreateTool.Execute(sc.Manager, new VisitorCreateRequest(items));
        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(3, batch.RequestedCount);
        Assert.Equal(3, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        foreach (var r in batch.Results)
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        }
    }
}
