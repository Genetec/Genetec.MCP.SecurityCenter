using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class UtilitiesPaginationToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("EntitySearchTool.SearchByName", 1)]
    [InlineData("EntitySearchTool.SearchByName", 2)]
    [InlineData("EntitySearchTool.SearchByName", 100)]
    [InlineData("EntitySearchTool", 1)]
    [InlineData("EntitySearchTool", 2)]
    [InlineData("EntitySearchTool", 100)]
    [InlineData("MapManagementTool", 1)]
    [InlineData("MapManagementTool", 2)]
    [InlineData("MapManagementTool", 100)]
    [InlineData("NetworkManagementTool", 1)]
    [InlineData("NetworkManagementTool", 2)]
    [InlineData("NetworkManagementTool", 100)]
    [InlineData("ZoneManagementTool", 1)]
    [InlineData("ZoneManagementTool", 2)]
    [InlineData("ZoneManagementTool", 100)]
    [InlineData("ReportAccessControlTool.AccessRuleConfiguration", 1)]
    [InlineData("ReportAccessControlTool.AccessRuleConfiguration", 2)]
    [InlineData("ReportAccessControlTool.AccessRuleConfiguration", 100)]
    [InlineData("ReportGeneralTool.UserGroup", 1)]
    [InlineData("ReportGeneralTool.UserGroup", 2)]
    [InlineData("ReportGeneralTool.UserGroup", 100)]
    public Task SmallPages_RespectLimitAndEntityIdentity(string tool, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc, (size, page) => tool switch
        {
            "EntitySearchTool.SearchByName" => EntitySearchTool.Execute(sc.Manager, new EntitySearchRequest(EntitySearchAction.SearchByName, EntityType: [ScEntityType.Camera], PageSize: size, Page: page)),
            "EntitySearchTool" => EntitySearchTool.Execute(sc.Manager, new EntitySearchRequest(EntitySearchAction.SearchByType, EntityType: [ScEntityType.Camera], PageSize: size, Page: page)),
            "MapManagementTool" => MapManagementTool.Execute(sc.Manager, new MapManagementRequest(MapAction.GetMaps, PageSize: size, Page: page)),
            "NetworkManagementTool" => NetworkManagementTool.Execute(sc.Manager, new NetworkManagementRequest(NetworkAction.List, PageSize: size, Page: page)),
            "ZoneManagementTool" => ZoneManagementTool.Execute(sc.Manager, new ZoneManagementRequest(ZoneAction.List, PageSize: size, Page: page)),
            "ReportAccessControlTool.AccessRuleConfiguration" => ReportAccessControlTool.Execute(sc.Manager, new ReportAccessControlRequest(AccessControlReportType.AccessRuleConfiguration, PageSize: size, Page: page)),
            "ReportGeneralTool.UserGroup" => ReportGeneralTool.Execute(sc.Manager, new ReportGeneralRequest(GeneralReportType.UserGroup, Filters: new ReportGeneralFilters(UserGroup: new UserGroupReportFilters(PageSize: size, Page: page)))),
            _ => throw new ArgumentException("Unknown pagination test case.", nameof(tool))
        }, pageSize, output.WriteLine);
}
