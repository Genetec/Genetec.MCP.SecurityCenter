using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_SingleGroup_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Create_BatchMode_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        const int count = 2;
        var items = Enumerable.Range(1, count)
            .Select(i => new UserGroupItem($"MCP_UG_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await UserGroupCreateTool.Execute(sc.Manager, new UserGroupCreateRequest(items));

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
