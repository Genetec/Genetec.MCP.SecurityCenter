using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task SingleCreate_ValidInputs_ReturnsGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var uniqueName = $"mcp_jdoe_{Guid.NewGuid():N}";
        var result = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(uniqueName)]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task SingleCreate_WithFirstAndLastName_ReturnsGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var uniqueName = $"mcp_asmith_{Guid.NewGuid():N}";
        const string firstName = "Alice, (literal) @name";
        var result = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(uniqueName, firstName, "Smith")]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        using var readback = JsonDocument.Parse(await sc.Manager.GetClient()
            .GetEntityFields(created.Guid.ToString(), "FirstName"));
        Assert.Equal(firstName, readback.RootElement.GetProperty("FirstName").GetString());
    }

    [Fact]
    public async Task BatchCreate_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        const int count = 2;
        const string firstName = "Batch, (literal) @name";
        var items = Enumerable.Range(1, count)
            .Select(i => new UserItem($"mcp_batch_{i}_{Guid.NewGuid():N}", firstName))
            .ToList();

        var result = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest(items));

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
        foreach (var created in batch.Results)
        {
            using var readback = JsonDocument.Parse(await sc.Manager.GetClient()
                .GetEntityFields(created.Guid!.Value.ToString(), "FirstName"));
            Assert.Equal(firstName, readback.RootElement.GetProperty("FirstName").GetString());
        }
    }
}
