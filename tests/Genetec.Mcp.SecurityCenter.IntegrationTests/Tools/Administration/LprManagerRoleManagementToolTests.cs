using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class LprManagerRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => LprManagerRoleManagementTool.Execute(sc.Manager, new LprManagerRoleManagementRequest(LprManagerRoleAction.Get, Id: id)), "id");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.List));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstListedRole_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listJson = (string)await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.List));
        var guid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.Get, Id: guid));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Create_IfNotExists_ReturnsValidGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listJson = (string)await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.List));
        var existingGuid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        if (existingGuid is not null)
        {
            var getResult = await LprManagerRoleManagementTool.Execute(sc.Manager,
                new LprManagerRoleManagementRequest(LprManagerRoleAction.Get, Id: existingGuid));
            Assert.NotNull(getResult);
            return;
        }

        var name = $"MCP_LprMgrRole_{Guid.NewGuid():N}";
        var result = await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.Create, Items: [name]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }
}
