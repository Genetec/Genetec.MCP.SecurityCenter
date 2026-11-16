using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class DirectoryManagerRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => DirectoryManagerRoleManagementTool.Execute(sc.Manager, new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get, Id: id)), "id");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var result = await DirectoryManagerRoleManagementTool.Execute(sc.Manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.List));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstListedRole_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var listJson = (string)await DirectoryManagerRoleManagementTool.Execute(sc.Manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.List));
        var guid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await DirectoryManagerRoleManagementTool.Execute(sc.Manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get, Id: guid));
        Assert.NotNull(result);
    }

}
