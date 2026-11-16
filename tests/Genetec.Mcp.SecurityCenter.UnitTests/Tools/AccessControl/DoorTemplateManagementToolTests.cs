using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class DoorTemplateManagementToolTests
{
    [Theory]
    [InlineData("door_id")]
    [InlineData("unit_id")]
    [InlineData("interface_module_id")]
    public async Task Apply_WrongLinkedType_RejectsBeforeMutation(string parameter)
    {
        var template = Guid.NewGuid();
        var door = Guid.NewGuid();
        var unit = Guid.NewGuid();
        var module = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(template, "DoorTemplate");
        handler.AddEntity(door, "Door");
        handler.AddEntity(unit, "Unit");
        handler.AddEntity(module, "InterfaceModule");
        handler.AddEntity(wrong, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(handler),
            new DoorTemplateManagementRequest(DoorTemplateAction.Apply, Id: template,
                DoorId: parameter == "door_id" ? wrong : door,
                UnitId: parameter == "unit_id" ? wrong : unit,
                InterfaceModuleId: parameter == "interface_module_id" ? wrong : module)));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<DoorTemplateAction>()
        .Where(action => action != DoorTemplateAction.List && action != DoorTemplateAction.Create).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(DoorTemplateAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(handler),
            (action is DoorTemplateAction.Update or DoorTemplateAction.Delete ? new DoorTemplateManagementRequest(action, Items: [new DoorTemplateManagementItem(Id: id, Body: action == DoorTemplateAction.Update ? new DoorTemplateUpdateProperties(Name: "blocked") : null)]) : new DoorTemplateManagementRequest(action, Id: id, DoorId: id, UnitId: id, InterfaceModuleId: id))));
        Assert.Equal(action is DoorTemplateAction.Update or DoorTemplateAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Get)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Update, Items: [new DoorTemplateManagementItem(Body: new DoorTemplateUpdateProperties(Name: "test"))])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullBody_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Update, Items: [new DoorTemplateManagementItem(Id: Guid.NewGuid())])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Delete)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_NullTemplateId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Apply,
                    DoorId: Guid.NewGuid(), UnitId: Guid.NewGuid(), InterfaceModuleId: Guid.NewGuid())));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_NullDoorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Apply,
                    Id: Guid.NewGuid(), UnitId: Guid.NewGuid(), InterfaceModuleId: Guid.NewGuid())));

        Assert.Contains("door_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_NullUnitId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Apply,
                    Id: Guid.NewGuid(), DoorId: Guid.NewGuid(), InterfaceModuleId: Guid.NewGuid())));

        Assert.Contains("unit_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_NullInterfaceModuleId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorTemplateManagementTool.Execute(TestHelpers.FakeManager(),
                new DoorTemplateManagementRequest(DoorTemplateAction.Apply,
                    Id: Guid.NewGuid(), DoorId: Guid.NewGuid(), UnitId: Guid.NewGuid())));

        Assert.Contains("interface_module_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_BuildsWebSdkMethodArguments()
    {
        var doorId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var interfaceModuleId = Guid.NewGuid();
        var request = new DoorTemplateManagementRequest(
            DoorTemplateAction.Apply,
            DoorId: doorId,
            UnitId: unitId,
            InterfaceModuleId: interfaceModuleId,
            ApplyAccessRules: true);

        Assert.Equal($"{doorId},{unitId},{interfaceModuleId},true",
            DoorTemplateManagementTool.BuildApplyArguments(request));
    }
}
