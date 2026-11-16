using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class RoleGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_FirstListedRole_ReturnsRequestedRoleAndName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listJson = await RoleListTool.Execute(sc.Manager, new RoleListRequest());
        var guid = ScIntegrationFixture.ExtractFirstGuid((string)listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await RoleGetTool.Execute(sc.Manager, new RoleGetTool.RoleGetRequest(guid!.Value));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(guid.Value, document.RootElement.GetProperty("Guid").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("Name").GetString()));
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => RoleGetTool.Execute(sc.Manager,
            new RoleGetTool.RoleGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
