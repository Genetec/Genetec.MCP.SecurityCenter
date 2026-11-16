using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_CreatedUser_ReturnsNameInResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_User_{Guid.NewGuid():N}";
        var createResult = await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var result = await UserGetTool.Execute(sc.Manager, new UserGetTool.UserGetRequest(created.Guid));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(created.Guid, document.RootElement.GetProperty("Guid").GetGuid());
        Assert.Equal(name, document.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserGetTool.Execute(sc.Manager,
            new UserGetTool.UserGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
