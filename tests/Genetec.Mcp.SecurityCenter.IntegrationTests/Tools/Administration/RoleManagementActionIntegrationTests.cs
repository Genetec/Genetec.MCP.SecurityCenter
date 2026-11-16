using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class RoleManagementActionIntegrationTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task AllRoleTools_ListAndGet_Succeed()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var accessManagerList = Assert.IsType<string>(await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List)));
        await AssertGet(await AccessManagerRoleManagementTool.Execute(sc.Manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Get,
                Id: RequireId(accessManagerList))));

        var activeDirectoryList = Assert.IsType<string>(await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.List)));
        await AssertGet(await ActiveDirectoryRoleManagementTool.Execute(sc.Manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Get,
                Id: RequireId(activeDirectoryList))));

        var archiverList = Assert.IsType<string>(await ArchiverRoleManagementTool.Execute(sc.Manager,
            new ArchiverRoleManagementRequest(ArchiverRoleAction.List)));
        await AssertGet(await ArchiverRoleManagementTool.Execute(sc.Manager,
            new ArchiverRoleManagementRequest(ArchiverRoleAction.Get,
                Id: RequireId(archiverList))));

        var authenticationServiceList = Assert.IsType<string>(await AuthenticationServiceRoleManagementTool.Execute(sc.Manager,
            new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.List)));
        await AssertGet(await AuthenticationServiceRoleManagementTool.Execute(sc.Manager,
            new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Get,
                Id: RequireId(authenticationServiceList))));

        var auxiliaryArchiverList = Assert.IsType<string>(await AuxiliaryArchiverRoleManagementTool.Execute(sc.Manager,
            new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.List)));
        await AssertGet(await AuxiliaryArchiverRoleManagementTool.Execute(sc.Manager,
            new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.Get,
                Id: RequireId(auxiliaryArchiverList))));

        var bwcManagerList = Assert.IsType<string>(await BwcManagerRoleManagementTool.Execute(sc.Manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.List)));
        await AssertGet(await BwcManagerRoleManagementTool.Execute(sc.Manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get,
                Id: RequireId(bwcManagerList))));

        var directoryManagerList = Assert.IsType<string>(await DirectoryManagerRoleManagementTool.Execute(sc.Manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.List)));
        await AssertGet(await DirectoryManagerRoleManagementTool.Execute(sc.Manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get,
                Id: RequireId(directoryManagerList))));

        var federationList = Assert.IsType<string>(await FederationRoleManagementTool.Execute(sc.Manager,
            new FederationRoleManagementRequest(FederationRoleAction.List)));
        await AssertGet(await FederationRoleManagementTool.Execute(sc.Manager,
            new FederationRoleManagementRequest(FederationRoleAction.Get,
                Id: RequireId(federationList))));

        var healthMonitorList = Assert.IsType<string>(await HealthMonitorRoleManagementTool.Execute(sc.Manager,
            new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.List)));
        await AssertGet(await HealthMonitorRoleManagementTool.Execute(sc.Manager,
            new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.Get,
                Id: RequireId(healthMonitorList))));

        var lprManagerList = Assert.IsType<string>(await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.List)));
        await AssertGet(await LprManagerRoleManagementTool.Execute(sc.Manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.Get,
                Id: RequireId(lprManagerList))));

        var mediaRouterList = Assert.IsType<string>(await MediaRouterRoleManagementTool.Execute(sc.Manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.List)));
        await AssertGet(await MediaRouterRoleManagementTool.Execute(sc.Manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.Get,
                Id: RequireId(mediaRouterList))));

        var intrusionList = Assert.IsType<string>(await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.List)));
        await AssertGet(await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Get,
                Id: RequireId(intrusionList))));
    }

    private static Guid RequireId(string json)
    {
        var id = ScIntegrationFixture.ExtractFirstGuid(json);
        Assert.True(id.HasValue, "Security Center returned no role GUID.");
        return id!.Value;
    }

    private static Task AssertGet(object result)
    {
        Assert.IsType<string>(result);
        return Task.CompletedTask;
    }

}
