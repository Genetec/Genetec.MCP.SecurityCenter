using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_RenameGroup_ReturnsStatusOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var created = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UGUpd_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(created.Guid);

        var result = await UserGroupUpdateTool.Execute(sc.Manager,
            new UserGroupUpdateRequest([new UserGroupUpdateItem(created.Guid, $"MCP_UGUpd_Renamed_{Guid.NewGuid():N}")]));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_BatchMode_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var guids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var created = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
                new UserGroupCreateRequest([new UserGroupItem($"MCP_UGUpd_{i}_{Guid.NewGuid():N}")]));
            guids.Add(created.Guid);
            sc.TrackForCleanup(created.Guid);
        }

        var items = guids
            .Select((id, i) => new UserGroupUpdateItem(id, $"MCP_UGUpd_Renamed_{i}_{Guid.NewGuid():N}"))
            .ToList();

        var result = await UserGroupUpdateTool.Execute(sc.Manager, new UserGroupUpdateRequest(items));
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_WrongType_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaCreateTool.Execute(sc.Manager,
            new Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaCreateRequest([new Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaItem(name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var request = new UserGroupUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new UserGroupUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => UserGroupUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await UserGroupUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Area", name);
    }
}
