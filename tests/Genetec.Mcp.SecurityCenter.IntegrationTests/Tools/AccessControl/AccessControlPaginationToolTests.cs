using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessControlPaginationToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("AccessControlUnitManagementTool", 1)]
    [InlineData("AccessControlUnitManagementTool", 2)]
    [InlineData("AccessControlUnitManagementTool", 100)]
    [InlineData("AccessRuleListTool", 1)]
    [InlineData("AccessRuleListTool", 2)]
    [InlineData("AccessRuleListTool", 100)]
    [InlineData("CardholderGroupManagementTool", 1)]
    [InlineData("CardholderGroupManagementTool", 2)]
    [InlineData("CardholderGroupManagementTool", 100)]
    [InlineData("CardholderListTool", 1)]
    [InlineData("CardholderListTool", 2)]
    [InlineData("CardholderListTool", 100)]
    [InlineData("CredentialListTool", 1)]
    [InlineData("CredentialListTool", 2)]
    [InlineData("CredentialListTool", 100)]
    [InlineData("DoorListTool", 1)]
    [InlineData("DoorListTool", 2)]
    [InlineData("DoorListTool", 100)]
    [InlineData("DoorTemplateManagementTool", 1)]
    [InlineData("DoorTemplateManagementTool", 2)]
    [InlineData("DoorTemplateManagementTool", 100)]
    [InlineData("ElevatorManagementTool", 1)]
    [InlineData("ElevatorManagementTool", 2)]
    [InlineData("ElevatorManagementTool", 100)]
    [InlineData("IntrusionDetectionRoleManagementTool", 1)]
    [InlineData("IntrusionDetectionRoleManagementTool", 2)]
    [InlineData("IntrusionDetectionRoleManagementTool", 100)]
    [InlineData("IntrusionUnitManagementTool", 1)]
    [InlineData("IntrusionUnitManagementTool", 2)]
    [InlineData("IntrusionUnitManagementTool", 100)]
    [InlineData("VisitorListTool", 1)]
    [InlineData("VisitorListTool", 2)]
    [InlineData("VisitorListTool", 100)]
    [InlineData("CardholderOperationsTool.SearchByStatus", 1)]
    [InlineData("CardholderOperationsTool.SearchByStatus", 2)]
    [InlineData("CardholderOperationsTool.SearchByStatus", 100)]
    [InlineData("CardholderOperationsTool.SearchByExpiration", 1)]
    [InlineData("CardholderOperationsTool.SearchByExpiration", 2)]
    [InlineData("CardholderOperationsTool.SearchByExpiration", 100)]
    [InlineData("CredentialSearchTool.FindUnassigned", 1)]
    [InlineData("CredentialSearchTool.FindUnassigned", 2)]
    [InlineData("CredentialSearchTool.FindUnassigned", 100)]
    public Task SmallPages_RespectLimitAndEntityIdentity(string tool, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc, (size, page) => tool switch
        {
            "AccessControlUnitManagementTool" => AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List, PageSize: size, Page: page)),
            "AccessRuleListTool" => AccessRuleListTool.Execute(sc.Manager, new AccessRuleListRequest(PageSize: size, Page: page)),
            "CardholderGroupManagementTool" => CardholderGroupManagementTool.Execute(sc.Manager, new CardholderGroupManagementRequest(CardholderGroupAction.List, PageSize: size, Page: page)),
            "CardholderListTool" => CardholderListTool.Execute(sc.Manager, new CardholderListRequest(PageSize: size, Page: page)),
            "CredentialListTool" => CredentialListTool.Execute(sc.Manager, new CredentialListRequest(PageSize: size, Page: page)),
            "DoorListTool" => DoorListTool.Execute(sc.Manager, new DoorListRequest(PageSize: size, Page: page)),
            "DoorTemplateManagementTool" => DoorTemplateManagementTool.Execute(sc.Manager, new DoorTemplateManagementRequest(DoorTemplateAction.List, PageSize: size, Page: page)),
            "ElevatorManagementTool" => ElevatorManagementTool.Execute(sc.Manager, new ElevatorManagementRequest(ElevatorAction.List, PageSize: size, Page: page)),
            "IntrusionDetectionRoleManagementTool" => IntrusionDetectionRoleManagementTool.Execute(sc.Manager, new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.List, PageSize: size, Page: page)),
            "IntrusionUnitManagementTool" => IntrusionUnitManagementTool.Execute(sc.Manager, new IntrusionUnitManagementRequest(IntrusionUnitAction.List, PageSize: size, Page: page)),
            "VisitorListTool" => VisitorListTool.Execute(sc.Manager, new VisitorListRequest(PageSize: size, Page: page)),
            "CardholderOperationsTool.SearchByStatus" => CardholderOperationsTool.Execute(sc.Manager, new CardholderOperationsRequest(CardholderOperationsAction.SearchByStatus, Status: AccessStatus.Active, PageSize: size, Page: page)),
            "CardholderOperationsTool.SearchByExpiration" => CardholderOperationsTool.Execute(sc.Manager, new CardholderOperationsRequest(CardholderOperationsAction.SearchByExpiration, PageSize: size, Page: page)),
            "CredentialSearchTool.FindUnassigned" => CredentialSearchTool.Execute(sc.Manager, new CredentialSearchRequest(CredentialSearchAction.FindUnassigned, PageSize: size, Page: page)),
            _ => throw new ArgumentException("Unknown pagination test case.", nameof(tool))
        }, pageSize, output.WriteLine);
}
