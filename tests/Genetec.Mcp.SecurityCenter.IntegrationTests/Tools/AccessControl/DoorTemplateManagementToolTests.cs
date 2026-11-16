using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorTemplateManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("door_id")]
    [InlineData("unit_id")]
    [InlineData("interface_module_id")]
    public async Task Apply_WrongLinkedType_RejectsAndPreservesTemplate(string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var fixture = await FindApplyFixtureAsync();
        Assert.True(fixture is not null, "Linked template validation requires an existing template, door, unit, and interface module.");
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, fixture!.TemplateId, "DoorTemplate", wrong =>
            DoorTemplateManagementTool.Execute(sc.Manager, new DoorTemplateManagementRequest(DoorTemplateAction.Apply,
                Id: fixture.TemplateId, DoorId: parameter == "door_id" ? wrong : fixture.DoorId,
                UnitId: parameter == "unit_id" ? wrong : fixture.UnitId,
                InterfaceModuleId: parameter == "interface_module_id" ? wrong : fixture.InterfaceModuleId)), parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<DoorTemplateAction>()
        .Where(action => action != DoorTemplateAction.List && action != DoorTemplateAction.Create).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(DoorTemplateAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorTemplateManagementTool.Execute(sc.Manager,
            (action is DoorTemplateAction.Update or DoorTemplateAction.Delete ? new DoorTemplateManagementRequest(action, Items: [new DoorTemplateManagementItem(Id: id, Body: action == DoorTemplateAction.Update ? new DoorTemplateUpdateProperties(Name: "blocked") : null)]) : new DoorTemplateManagementRequest(action, Id: id, DoorId: id, UnitId: id, InterfaceModuleId: id))));
        Assert.Equal(action is DoorTemplateAction.Update or DoorTemplateAction.Delete ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.List));

        Assert.IsType<string>(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_DoorTemplate_{Guid.NewGuid():N}";

        // Create
        var createResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [new DoorTemplateManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        // Get
        var getResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Get, Id: created.Guid));

        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        // Update
        var newName = $"{name}_Updated";
        var updateResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Update, Items: [new DoorTemplateManagementItem(Id: created.Guid, Body: new DoorTemplateUpdateProperties(Name: newName))]));

        Assert.NotNull(updateResult);

        // Delete
        var deleteResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Delete, Items: [new DoorTemplateManagementItem(Id: created.Guid)]));

        Assert.NotNull(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task GetAccessPoints_ExistingTemplate_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_DoorTemplate_{Guid.NewGuid():N}";
        var createResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [new DoorTemplateManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.GetAccessPoints, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAccessRules_ExistingTemplate_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_DoorTemplate_{Guid.NewGuid():N}";
        var createResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [new DoorTemplateManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.GetAccessRules, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCardPinSettings_ExistingTemplate_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_DoorTemplate_{Guid.NewGuid():N}";
        var createResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [new DoorTemplateManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.GetCardPinSettings, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAppliedDoors_ExistingTemplate_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_DoorTemplate_{Guid.NewGuid():N}";
        var createResult = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.Create, Items: [new DoorTemplateManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager,
            new DoorTemplateManagementRequest(DoorTemplateAction.GetAppliedDoors, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Apply_WithDoorUnitAndInterface_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var fixture = await FindApplyFixtureAsync();
        Assert.True(fixture is not null,
            "Door-template apply integration requires a configured Unit with at least one Door and InterfaceModule.");

        var request = new DoorTemplateManagementRequest(
            DoorTemplateAction.Apply,
            Id: fixture!.TemplateId,
            DoorId: fixture.DoorId,
            UnitId: fixture.UnitId,
            InterfaceModuleId: fixture.InterfaceModuleId);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager, request);

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Apply_WithAccessRulesFlag_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var fixture = await FindApplyFixtureAsync();
        Assert.True(fixture is not null,
            "Door-template apply integration requires a configured Unit with at least one Door and InterfaceModule.");

        var request = new DoorTemplateManagementRequest(
            DoorTemplateAction.Apply,
            Id: fixture!.TemplateId,
            DoorId: fixture.DoorId,
            UnitId: fixture.UnitId,
            InterfaceModuleId: fixture.InterfaceModuleId,
            ApplyAccessRules: true);

        var result = await DoorTemplateManagementTool.Execute(sc.Manager, request);

        Assert.IsType<string>(result);
    }

    private async Task<DoorTemplateApplyFixture?> FindApplyFixtureAsync()
    {
        var client = sc.Manager.GetClient();
        var templateList = await client.ListEntities("DoorTemplate", 1, 1, ["Name"]);
        var templateId = IntegrationTestHelpers.ExtractFirstGuid(templateList);
        if (templateId is null)
            return null;

        var unitList = await client.ListEntities("Unit", pageSize: 100, page: 1, returnFields: ["Name"]);
        foreach (var unitId in IntegrationTestHelpers.ExtractGuids(unitList))
        {
            var doorIds = IntegrationTestHelpers.ExtractGuids(
                await client.GetEntityField(unitId.ToString(), "Doors"));
            var interfaceModuleIds = IntegrationTestHelpers.ExtractGuids(
                await client.GetEntityField(unitId.ToString(), "InterfaceModules"));

            if (doorIds.Count > 0 && interfaceModuleIds.Count > 0)
            {
                return new DoorTemplateApplyFixture(
                    templateId.Value, doorIds[0], unitId, interfaceModuleIds[0]);
            }
        }

        return null;
    }

    private sealed record DoorTemplateApplyFixture(
        Guid TemplateId,
        Guid DoorId,
        Guid UnitId,
        Guid InterfaceModuleId);
}
