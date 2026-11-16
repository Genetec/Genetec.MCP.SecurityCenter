using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessRuleCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_Single_PermanentRule_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_AccessRule_{Guid.NewGuid():N}";
        var result = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem(name)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Create_Single_TemporaryRule_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_TempRule_{Guid.NewGuid():N}";
        var result = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem(name,
                StartTime: "2024-01-01T08:00:00Z", EndTime: "2024-12-31T18:00:00Z")]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Create_Batch_PermanentRules_ReturnsBatchCreateResult(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = Enumerable.Range(1, count)
            .Select(i => new AccessRuleItem($"MCP_AccessRule_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await AccessRuleCreateTool.Execute(sc.Manager, new AccessRuleCreateRequest(items));
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

    [Fact]
    public async Task Create_Batch_TemporaryRules_ReturnsBatchCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = new List<AccessRuleItem>
        {
            new($"MCP_TempRule_{Guid.NewGuid():N}", StartTime: "2024-01-01T08:00:00Z", EndTime: "2024-06-30T18:00:00Z"),
            new($"MCP_TempRule_{Guid.NewGuid():N}", StartTime: "2024-07-01T08:00:00Z", EndTime: "2024-12-31T18:00:00Z")
        };

        var result = await AccessRuleCreateTool.Execute(sc.Manager, new AccessRuleCreateRequest(items));
        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(2, batch.RequestedCount);
        Assert.Equal(2, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        foreach (var r in batch.Results)
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        }
    }
}
