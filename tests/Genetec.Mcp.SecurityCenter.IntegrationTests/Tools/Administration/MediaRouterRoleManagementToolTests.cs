using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class MediaRouterRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => MediaRouterRoleManagementTool.Execute(sc.Manager, new MediaRouterRoleManagementRequest(MediaRouterRoleAction.Get, Id: id)), "id");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var result = await MediaRouterRoleManagementTool.Execute(sc.Manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.List));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstListedRole_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var listJson = (string)await MediaRouterRoleManagementTool.Execute(sc.Manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.List));
        var guid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await MediaRouterRoleManagementTool.Execute(sc.Manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.Get, Id: guid));
        Assert.NotNull(result);
    }

}
