using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Visitor_{Guid.NewGuid():N}";
        var createResult = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await VisitorUpdateTool.Execute(sc.Manager,
            new VisitorUpdateRequest([new VisitorUpdateItem(created.Guid, $"{name}_Updated")]));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name1 = $"MCP_Visitor_{Guid.NewGuid():N}";
        var name2 = $"MCP_Visitor_{Guid.NewGuid():N}";

        var create1 = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem(name1)]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);
        sc.TrackForCleanup(created1.Guid);

        var create2 = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem(name2)]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created2.Guid);

        var result = await VisitorUpdateTool.Execute(sc.Manager,
            new VisitorUpdateRequest([
                new VisitorUpdateItem(created1.Guid, $"{name1}_Updated"),
                new VisitorUpdateItem(created2.Guid, $"{name2}_Updated")]));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new VisitorUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new VisitorUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await VisitorUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
