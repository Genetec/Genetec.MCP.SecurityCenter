using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Area_{Guid.NewGuid():N}";
        var createResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AreaUpdateTool.Execute(sc.Manager,
            new AreaUpdateRequest([new AreaUpdateItem(created.Guid, $"MCP_Area_Updated_{Guid.NewGuid():N}")]));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name1 = $"MCP_Area_{Guid.NewGuid():N}";
        var name2 = $"MCP_Area_{Guid.NewGuid():N}";

        var create1 = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name1)]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);
        sc.TrackForCleanup(created1.Guid);

        var create2 = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(name2)]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created2.Guid);

        var result = await AreaUpdateTool.Execute(sc.Manager,
            new AreaUpdateRequest([
                new AreaUpdateItem(created1.Guid, $"MCP_Area_Upd_{Guid.NewGuid():N}"),
                new AreaUpdateItem(created2.Guid, $"MCP_Area_Upd_{Guid.NewGuid():N}")]));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new AreaUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new AreaUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await AreaUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
