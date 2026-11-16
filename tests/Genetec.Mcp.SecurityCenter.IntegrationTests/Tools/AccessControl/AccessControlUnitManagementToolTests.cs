using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessControlUnitManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(AcuAction.AddInterfaceModule, "interface_module_id")]
    [InlineData(AcuAction.Move, "target_unit_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(AcuAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listed = await sc.Manager.GetClient().ListEntities("Unit", 1, 1);
        var selectedId = ScIntegrationFixture.ExtractFirstGuid(listed);
        Assert.True(selectedId.HasValue, "Linked unit validation requires an existing access control unit.");
        var id = selectedId.Value;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Unit", wrong => (action, parameter) switch
        {
            (AcuAction.AddInterfaceModule, "interface_module_id") => AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(action, Id: id, InterfaceModuleId: wrong)),
            (AcuAction.Move, "target_unit_id") => AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(action, Id: id, TargetUnitId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AcuAction>()
        .Where(action => action != AcuAction.List).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(AcuAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessControlUnitManagementTool.Execute(sc.Manager,
            (action is AcuAction.Update ? new AcuManagementRequest(action, Items: [new AcuManagementItem(Id: id, Body: action == AcuAction.Update ? new AccessControlUnitUpdateProperties(Name: "Must not update") : null)]) : new AcuManagementRequest(action, Id: id, TargetUnitId: id, InterfaceModuleId: id))));
        Assert.Equal(action is AcuAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Get_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No access control Unit is configured in Security Center. This test requires an enrolled access control unit.");
        var result = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.Get, Id: id));
        Assert.IsType<string>(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetDoors_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No access control Unit is configured in Security Center. This test requires an enrolled access control unit.");
        var result = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.GetDoors, Id: id));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task GetDevices_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No access control Unit is configured in Security Center. This test requires an enrolled access control unit.");
        var result = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.GetDevices, Id: id));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task GetInterfaceModules_ValidId_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var list = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.List));
        var id = ScIntegrationFixture.ExtractFirstGuid((string)list);
        Assert.True(id.HasValue, "No access control Unit is configured in Security Center. This test requires an enrolled access control unit.");
        var result = await AccessControlUnitManagementTool.Execute(sc.Manager, new AcuManagementRequest(AcuAction.GetInterfaceModules, Id: id));
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_WithTypedBody_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => AccessControlUnitManagementTool.Execute(sc.Manager,
            new AcuManagementRequest(AcuAction.Update, Items: [new AcuManagementItem(Id: Guid.NewGuid(), Body: new AccessControlUnitUpdateProperties(Name: "MCP_Unit_Update"))])));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Fact]
    public async Task Update_WithNameShortcut_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => AccessControlUnitManagementTool.Execute(sc.Manager,
            new AcuManagementRequest(AcuAction.Update, Items: [new AcuManagementItem(Name: "MCP_Unit_Update", Id: Guid.NewGuid())])));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Fact]
    public async Task AddInterfaceModule_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => AccessControlUnitManagementTool.Execute(sc.Manager,
            new AcuManagementRequest(AcuAction.AddInterfaceModule,
                Id: Guid.NewGuid(), InterfaceModuleId: Guid.NewGuid())));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Fact]
    public async Task Move_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => AccessControlUnitManagementTool.Execute(sc.Manager,
            new AcuManagementRequest(AcuAction.Move,
                Id: Guid.NewGuid(), TargetUnitId: Guid.NewGuid())));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }
}
