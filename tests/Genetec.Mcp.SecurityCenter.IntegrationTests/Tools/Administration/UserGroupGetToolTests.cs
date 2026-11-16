using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_CreatedUserGroup_ReturnsNameInResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_UG_{Guid.NewGuid():N}";
        var createResult = await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var result = await UserGroupGetTool.Execute(sc.Manager, new UserGroupGetTool.UserGroupGetRequest(created.Guid));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(created.Guid, document.RootElement.GetProperty("Guid").GetGuid());
        Assert.Equal(name, document.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_WrongType_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaCreateTool.Execute(sc.Manager,
            new Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaCreateRequest([new Genetec.Mcp.SecurityCenter.Tools.AccessControl.AreaItem(name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserGroupGetTool.Execute(sc.Manager,
            new UserGroupGetTool.UserGroupGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Area", name);
    }
}
