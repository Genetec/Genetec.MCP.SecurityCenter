using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class LprPaginationToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("LprHotlistTool", 1)]
    [InlineData("LprHotlistTool", 2)]
    [InlineData("LprHotlistTool", 100)]
    [InlineData("MlpiRuleManagementTool", 1)]
    [InlineData("MlpiRuleManagementTool", 2)]
    [InlineData("MlpiRuleManagementTool", 100)]
    [InlineData("OvertimeRuleManagementTool", 1)]
    [InlineData("OvertimeRuleManagementTool", 2)]
    [InlineData("OvertimeRuleManagementTool", 100)]
    [InlineData("ParkingRuleTool", 1)]
    [InlineData("ParkingRuleTool", 2)]
    [InlineData("ParkingRuleTool", 100)]
    [InlineData("ParkingZoneTool", 1)]
    [InlineData("ParkingZoneTool", 2)]
    [InlineData("ParkingZoneTool", 100)]
    [InlineData("PermitManagementTool", 1)]
    [InlineData("PermitManagementTool", 2)]
    [InlineData("PermitManagementTool", 100)]
    [InlineData("PermitRuleManagementTool", 1)]
    [InlineData("PermitRuleManagementTool", 2)]
    [InlineData("PermitRuleManagementTool", 100)]
    public Task SmallPages_RespectLimitAndEntityIdentity(string tool, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc, (size, page) => tool switch
        {
            "LprHotlistTool" => LprHotlistTool.Execute(sc.Manager, new LprHotlistManagementRequest(LprHotlistAction.List, PageSize: size, Page: page)),
            "MlpiRuleManagementTool" => MlpiRuleManagementTool.Execute(sc.Manager, new MlpiRuleManagementRequest(MlpiRuleAction.List, PageSize: size, Page: page)),
            "OvertimeRuleManagementTool" => OvertimeRuleManagementTool.Execute(sc.Manager, new OvertimeRuleManagementRequest(OvertimeRuleAction.List, PageSize: size, Page: page)),
            "ParkingRuleTool" => ParkingRuleTool.Execute(sc.Manager, new ParkingRuleManagementRequest(ParkingRuleAction.List, PageSize: size, Page: page)),
            "ParkingZoneTool" => ParkingZoneTool.Execute(sc.Manager, new ParkingZoneManagementRequest(ParkingZoneAction.List, PageSize: size, Page: page)),
            "PermitManagementTool" => PermitManagementTool.Execute(sc.Manager, new PermitManagementRequest(PermitAction.List, PageSize: size, Page: page)),
            "PermitRuleManagementTool" => PermitRuleManagementTool.Execute(sc.Manager, new PermitRuleManagementRequest(PermitRuleAction.List, PageSize: size, Page: page)),
            _ => throw new ArgumentException("Unknown pagination test case.", nameof(tool))
        }, pageSize, output.WriteLine);
}
