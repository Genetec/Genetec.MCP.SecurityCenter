using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_SingleCardholder_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";

        var result = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Create_SingleCardholder_WithOptionalFields_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";

        var result = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name, "John", "Doe", "john.doe@example.com")]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Create_BatchCardholders_ReturnsBatchCreateResult(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = Enumerable.Range(1, count)
            .Select(i => new CardholderItem($"MCP_Cardholder_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest(items));

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
