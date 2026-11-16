using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AccessControlUnitManagementToolTests
{
    [Theory]
    [InlineData(AcuAction.AddInterfaceModule, "interface_module_id")]
    [InlineData(AcuAction.Move, "target_unit_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(AcuAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Unit");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (AcuAction.AddInterfaceModule, "interface_module_id") => (action is AcuAction.Update ? new AcuManagementRequest(action, Items: [new AcuManagementItem(Id: id, Body: action == AcuAction.Update ? new AccessControlUnitUpdateProperties(Name: "Must not update") : null)]) : new AcuManagementRequest(action, Id: id, InterfaceModuleId: wrong)),
            (AcuAction.Move, "target_unit_id") => (action is AcuAction.Update ? new AcuManagementRequest(action, Items: [new AcuManagementItem(Id: id, Body: action == AcuAction.Update ? new AccessControlUnitUpdateProperties(Name: "Must not update") : null)]) : new AcuManagementRequest(action, Id: id, TargetUnitId: wrong)),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessControlUnitManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AcuAction>()
        .Where(action => action != AcuAction.List).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(AcuAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessControlUnitManagementTool.Execute(TestHelpers.FakeManager(handler),
            (action is AcuAction.Update ? new AcuManagementRequest(action, Items: [new AcuManagementItem(Id: id, Body: action == AcuAction.Update ? new AccessControlUnitUpdateProperties(Name: "Must not update") : null)]) : new AcuManagementRequest(action, Id: id, TargetUnitId: id, InterfaceModuleId: id))));
        Assert.Equal(action is AcuAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Move_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessControlUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new AcuManagementRequest(AcuAction.Move, Id: null, TargetUnitId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Move_NullTargetUnitId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessControlUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new AcuManagementRequest(AcuAction.Move, Id: Guid.NewGuid(), TargetUnitId: null)));

        Assert.Contains("target_unit_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddInterfaceModule_NullModuleId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AccessControlUnitManagementTool.Execute(TestHelpers.FakeManager(),
                new AcuManagementRequest(AcuAction.AddInterfaceModule, Id: Guid.NewGuid(), InterfaceModuleId: null)));

        Assert.Contains("interface_module_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
