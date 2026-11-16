using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AdministrationPaginationToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("AccessManagerRoleManagementTool", 1)]
    [InlineData("AccessManagerRoleManagementTool", 2)]
    [InlineData("AccessManagerRoleManagementTool", 100)]
    [InlineData("ActiveDirectoryRoleManagementTool", 1)]
    [InlineData("ActiveDirectoryRoleManagementTool", 2)]
    [InlineData("ActiveDirectoryRoleManagementTool", 100)]
    [InlineData("AlarmListTool", 1)]
    [InlineData("AlarmListTool", 2)]
    [InlineData("AlarmListTool", 100)]
    [InlineData("ArchiverRoleManagementTool", 1)]
    [InlineData("ArchiverRoleManagementTool", 2)]
    [InlineData("ArchiverRoleManagementTool", 100)]
    [InlineData("AuthenticationServiceRoleManagementTool", 1)]
    [InlineData("AuthenticationServiceRoleManagementTool", 2)]
    [InlineData("AuthenticationServiceRoleManagementTool", 100)]
    [InlineData("AuxiliaryArchiverRoleManagementTool", 1)]
    [InlineData("AuxiliaryArchiverRoleManagementTool", 2)]
    [InlineData("AuxiliaryArchiverRoleManagementTool", 100)]
    [InlineData("BwcManagerRoleManagementTool", 1)]
    [InlineData("BwcManagerRoleManagementTool", 2)]
    [InlineData("BwcManagerRoleManagementTool", 100)]
    [InlineData("DirectoryManagerRoleManagementTool", 1)]
    [InlineData("DirectoryManagerRoleManagementTool", 2)]
    [InlineData("DirectoryManagerRoleManagementTool", 100)]
    [InlineData("FederationRoleManagementTool", 1)]
    [InlineData("FederationRoleManagementTool", 2)]
    [InlineData("FederationRoleManagementTool", 100)]
    [InlineData("HealthMonitorRoleManagementTool", 1)]
    [InlineData("HealthMonitorRoleManagementTool", 2)]
    [InlineData("HealthMonitorRoleManagementTool", 100)]
    [InlineData("LprManagerRoleManagementTool", 1)]
    [InlineData("LprManagerRoleManagementTool", 2)]
    [InlineData("LprManagerRoleManagementTool", 100)]
    [InlineData("MacroManagementTool", 1)]
    [InlineData("MacroManagementTool", 2)]
    [InlineData("MacroManagementTool", 100)]
    [InlineData("MediaRouterRoleManagementTool", 1)]
    [InlineData("MediaRouterRoleManagementTool", 2)]
    [InlineData("MediaRouterRoleManagementTool", 100)]
    [InlineData("PartitionManagementTool", 1)]
    [InlineData("PartitionManagementTool", 2)]
    [InlineData("PartitionManagementTool", 100)]
    [InlineData("RoleListTool", 1)]
    [InlineData("RoleListTool", 2)]
    [InlineData("RoleListTool", 100)]
    [InlineData("ScheduleManagementTool", 1)]
    [InlineData("ScheduleManagementTool", 2)]
    [InlineData("ScheduleManagementTool", 100)]
    [InlineData("UserGroupListTool", 1)]
    [InlineData("UserGroupListTool", 2)]
    [InlineData("UserGroupListTool", 100)]
    [InlineData("UserListTool", 1)]
    [InlineData("UserListTool", 2)]
    [InlineData("UserListTool", 100)]
    public Task SmallPages_RespectLimitAndEntityIdentity(string tool, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc, (size, page) => tool switch
        {
            "AccessManagerRoleManagementTool" => AccessManagerRoleManagementTool.Execute(sc.Manager, new AccessManagerRoleManagementRequest(AccessManagerRoleAction.List, PageSize: size, Page: page)),
            "ActiveDirectoryRoleManagementTool" => ActiveDirectoryRoleManagementTool.Execute(sc.Manager, new ActiveDirectoryRoleManagementRequest(ActiveDirectoryRoleAction.List, PageSize: size, Page: page)),
            "AlarmListTool" => AlarmListTool.Execute(sc.Manager, new AlarmListRequest(PageSize: size, Page: page)),
            "ArchiverRoleManagementTool" => ArchiverRoleManagementTool.Execute(sc.Manager, new ArchiverRoleManagementRequest(ArchiverRoleAction.List, PageSize: size, Page: page)),
            "AuthenticationServiceRoleManagementTool" => AuthenticationServiceRoleManagementTool.Execute(sc.Manager, new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.List, PageSize: size, Page: page)),
            "AuxiliaryArchiverRoleManagementTool" => AuxiliaryArchiverRoleManagementTool.Execute(sc.Manager, new AuxiliaryArchiverRoleManagementRequest(AuxiliaryArchiverRoleAction.List, PageSize: size, Page: page)),
            "BwcManagerRoleManagementTool" => BwcManagerRoleManagementTool.Execute(sc.Manager, new BwcManagerRoleManagementRequest(BwcManagerRoleAction.List, PageSize: size, Page: page)),
            "DirectoryManagerRoleManagementTool" => DirectoryManagerRoleManagementTool.Execute(sc.Manager, new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.List, PageSize: size, Page: page)),
            "FederationRoleManagementTool" => FederationRoleManagementTool.Execute(sc.Manager, new FederationRoleManagementRequest(FederationRoleAction.List, PageSize: size, Page: page)),
            "HealthMonitorRoleManagementTool" => HealthMonitorRoleManagementTool.Execute(sc.Manager, new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.List, PageSize: size, Page: page)),
            "LprManagerRoleManagementTool" => LprManagerRoleManagementTool.Execute(sc.Manager, new LprManagerRoleManagementRequest(LprManagerRoleAction.List, PageSize: size, Page: page)),
            "MacroManagementTool" => MacroManagementTool.Execute(sc.Manager, new MacroManagementRequest(MacroAction.List, PageSize: size, Page: page)),
            "MediaRouterRoleManagementTool" => MediaRouterRoleManagementTool.Execute(sc.Manager, new MediaRouterRoleManagementRequest(MediaRouterRoleAction.List, PageSize: size, Page: page)),
            "PartitionManagementTool" => PartitionManagementTool.Execute(sc.Manager, new PartitionManagementRequest(PartitionAction.List, PageSize: size, Page: page)),
            "RoleListTool" => RoleListTool.Execute(sc.Manager, new RoleListRequest(PageSize: size, Page: page)),
            "ScheduleManagementTool" => ScheduleManagementTool.Execute(sc.Manager, new ScheduleManagementRequest(ScheduleAction.List, PageSize: size, Page: page)),
            "UserGroupListTool" => UserGroupListTool.Execute(sc.Manager, new UserGroupListRequest(PageSize: size, Page: page)),
            "UserListTool" => UserListTool.Execute(sc.Manager, new UserListRequest(PageSize: size, Page: page)),
            _ => throw new ArgumentException("Unknown pagination test case.", nameof(tool))
        }, pageSize, output.WriteLine);
}
