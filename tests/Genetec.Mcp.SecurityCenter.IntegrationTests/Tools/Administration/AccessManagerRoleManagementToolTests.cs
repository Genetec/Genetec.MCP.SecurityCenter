using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AccessManagerRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => AccessManagerRoleManagementTool.Execute(sc.Manager, new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Get, Id: id)), "id");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstListedRole_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listJson = (string)await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List));
        var guid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        Assert.True(guid is not null, "No roles found in Security Center.");

        var result = await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Get, Id: guid));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Create_IfNotExists_ReturnsValidGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var listJson = (string)await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List));
        var existingGuid = ScIntegrationFixture.ExtractFirstGuid(listJson);
        if (existingGuid is not null)
        {
            var getResult = await AccessManagerRoleManagementTool.Execute(sc.Manager,
                new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Get, Id: existingGuid));
            Assert.NotNull(getResult);
            return;
        }

        var name = $"MCP_AMRole_{Guid.NewGuid():N}";
        var result = await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Create, Items: [name]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Create_WithServerId_AddsServerForSingleAndBatchRoles()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var serverId = await sc.GetServerIdAsync();
        Assert.True(serverId.HasValue, "No server found in Security Center.");

        var singleName = $"MCP_AMRole_SrvId_{Guid.NewGuid():N}";
        var singleResult = await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Create, Items: [singleName], ServerId: serverId));
        var singleCreated = Assert.IsType<EntityCreateResult>(singleResult);
        Assert.NotEqual(default(Guid), singleCreated.Guid);
        sc.TrackForCleanup(singleCreated.Guid);

        var singleReadback = await sc.Manager.GetClient().GetEntityFields(singleCreated.Guid.ToString(), "RoleServers");
        Assert.True(singleReadback.Contains(serverId.Value.ToString(), StringComparison.OrdinalIgnoreCase),
            $"RoleServers did not contain server {serverId.Value}: {singleReadback}");

        var batchNames = new List<string>
        {
            $"MCP_AMRole_SrvId_Batch1_{Guid.NewGuid():N}",
            $"MCP_AMRole_SrvId_Batch2_{Guid.NewGuid():N}"
        };
        var batchResult = await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Create, Items: batchNames, ServerId: serverId));
        var batchCreated = Assert.IsType<BatchCreateResult>(batchResult);
        Assert.Equal(batchNames.Count, batchCreated.SuccessCount);
        Assert.All(batchCreated.Results, item =>
        {
            Assert.True(item.Success);
            Assert.True(item.Guid.HasValue);
            sc.TrackForCleanup(item.Guid);
        });

        foreach (var item in batchCreated.Results)
        {
            var readback = await sc.Manager.GetClient().GetEntityFields(item.Guid!.Value.ToString(), "RoleServers");
            Assert.True(readback.Contains(serverId.Value.ToString(), StringComparison.OrdinalIgnoreCase),
                $"RoleServers did not contain server {serverId.Value}: {readback}");
        }
    }
}
