using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class RoleManagementActionTests
{
    [Fact]
    public async Task AllSupportedActions_DispatchToSecurityCenter()
    {
        var handler = new EntityMetadataTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var id = Guid.NewGuid();
        handler.AddEntity(id, "Role");

        async Task Run(Func<Task<object>> action) => Assert.NotNull(await action());

        await Run(() => AccessManagerRoleManagementTool.Execute(manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List)));
        await Run(() => AccessManagerRoleManagementTool.Execute(manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Get, Id: id)));
        await Run(() => AccessManagerRoleManagementTool.Execute(manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Create, Items: ["AccessManager"])));
        await Run(() => AccessManagerRoleManagementTool.Execute(manager,
            new AccessManagerRoleManagementRequest(AccessManagerRoleAction.Create,
                Items: ["AccessManagerWithServer"], ServerId: id)));
        var addServerUri = handler.Requests[^1].Uri;
        var addServerQuery = Uri.UnescapeDataString(addServerUri.Query);
        Assert.Contains($"AddServer({id})", addServerQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("Server=", addServerQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerId=", addServerQuery, StringComparison.Ordinal);
        Assert.Contains("%3D", addServerUri.Query, StringComparison.OrdinalIgnoreCase);

        await Run(() => ActiveDirectoryRoleManagementTool.Execute(manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.List)));
        await Run(() => ActiveDirectoryRoleManagementTool.Execute(manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Get, Id: id)));
        await Run(() => ActiveDirectoryRoleManagementTool.Execute(manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Create, Items: ["ActiveDirectory"])));
        await Run(() => ActiveDirectoryRoleManagementTool.Execute(manager,
            new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.Sync, Id: id)));

        await Run(() => ArchiverRoleManagementTool.Execute(manager,
            new ArchiverRoleManagementRequest(ArchiverRoleAction.List)));
        await Run(() => ArchiverRoleManagementTool.Execute(manager,
            new ArchiverRoleManagementRequest(ArchiverRoleAction.Get, Id: id)));
        await Run(() => ArchiverRoleManagementTool.Execute(manager,
            new ArchiverRoleManagementRequest(ArchiverRoleAction.Create, Items: ["Archiver"])));

        await Run(() => AuthenticationServiceRoleManagementTool.Execute(manager,
            new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.List)));
        await Run(() => AuthenticationServiceRoleManagementTool.Execute(manager,
            new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Get, Id: id)));
        await Run(() => AuthenticationServiceRoleManagementTool.Execute(manager,
            new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Create, Items: ["AuthenticationService"])));

        await Run(() => AuxiliaryArchiverRoleManagementTool.Execute(manager,
            new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.List)));
        await Run(() => AuxiliaryArchiverRoleManagementTool.Execute(manager,
            new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.Get, Id: id)));
        await Run(() => AuxiliaryArchiverRoleManagementTool.Execute(manager,
            new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.Create, Items: ["AuxiliaryArchiver"])));

        await Run(() => BwcManagerRoleManagementTool.Execute(manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.List)));
        await Run(() => BwcManagerRoleManagementTool.Execute(manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Get, Id: id)));
        await Run(() => BwcManagerRoleManagementTool.Execute(manager,
            new BwcManagerRoleManagementRequest(BwcManagerRoleAction.Create, Items: ["BwcManager"])));

        await Run(() => DirectoryManagerRoleManagementTool.Execute(manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.List)));
        await Run(() => DirectoryManagerRoleManagementTool.Execute(manager,
            new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get, Id: id)));

        await Run(() => FederationRoleManagementTool.Execute(manager,
            new FederationRoleManagementRequest(FederationRoleAction.List)));
        await Run(() => FederationRoleManagementTool.Execute(manager,
            new FederationRoleManagementRequest(FederationRoleAction.Get, Id: id)));
        await Run(() => FederationRoleManagementTool.Execute(manager,
            new FederationRoleManagementRequest(FederationRoleAction.Create, Items: ["Federation"])));

        await Run(() => HealthMonitorRoleManagementTool.Execute(manager,
            new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.List)));
        await Run(() => HealthMonitorRoleManagementTool.Execute(manager,
            new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.Get, Id: id)));
        await Run(() => HealthMonitorRoleManagementTool.Execute(manager,
            new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.GetEventReport)));

        await Run(() => LprManagerRoleManagementTool.Execute(manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.List)));
        await Run(() => LprManagerRoleManagementTool.Execute(manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.Get, Id: id)));
        await Run(() => LprManagerRoleManagementTool.Execute(manager,
            new LprManagerRoleManagementRequest(LprManagerRoleAction.Create, Items: ["LprManager"])));

        await Run(() => MediaRouterRoleManagementTool.Execute(manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.List)));
        await Run(() => MediaRouterRoleManagementTool.Execute(manager,
            new MediaRouterRoleManagementRequest(MediaRouterRoleAction.Get, Id: id)));

        await Run(() => IntrusionDetectionRoleManagementTool.Execute(manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.List)));
        await Run(() => IntrusionDetectionRoleManagementTool.Execute(manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Get, Id: id)));
        await Run(() => IntrusionDetectionRoleManagementTool.Execute(manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create, Items: ["IntrusionDetection"])));

        var metadataRequests = handler.Requests.Where(request =>
            Uri.UnescapeDataString(request.Uri.Query).EndsWith(",Guid,EntityType", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, metadataRequests.Count); // ActiveDirectory sync and IntrusionDetection get.
        Assert.All(metadataRequests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Equal(36, handler.Requests.Count - metadataRequests.Count);
    }

    [Fact]
    public void RoleActions_DoNotExposeDelete()
    {
        Assert.DoesNotContain("Delete", Enum.GetNames<AccessManagerRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<ActiveDirectoryRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<ArchiverRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<AuthenticationServiceRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<AuxiliaryArchiverRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<BwcManagerRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<DirectoryManagerRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<FederationRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<HealthMonitorRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<LprManagerRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<MediaRouterRoleAction>());
        Assert.DoesNotContain("Delete", Enum.GetNames<IntrusionDetectionRoleAction>());
        Assert.DoesNotContain("Create", Enum.GetNames<DirectoryManagerRoleAction>());
        Assert.DoesNotContain("Create", Enum.GetNames<HealthMonitorRoleAction>());
        Assert.DoesNotContain("Create", Enum.GetNames<MediaRouterRoleAction>());
        Assert.DoesNotContain("DirectoryManager", Enum.GetNames<ScRoleType>());
        Assert.DoesNotContain("HealthMonitor", Enum.GetNames<ScRoleType>());
        Assert.DoesNotContain("MediaRouter", Enum.GetNames<ScRoleType>());
    }
}
