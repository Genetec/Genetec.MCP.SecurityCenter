using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class EntitySearchToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> EntityTypes()
        => Enum.GetValues<ScEntityType>().Select(value => new object[] { value });

    public static IEnumerable<object[]> NameSearchModes()
        => Enum.GetValues<EntityNameSearchMode>().Select(value => new object[] { value });

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData("%2CGuidsOnly%3DTrue")]
    [InlineData("#literal")]
    [InlineData("+literal")]
    [InlineData(@", (literal) @site\item")]
    public async Task SearchByName_LiteralQueryCharacters_ReturnsOnlyMatchingEntity(string suffix)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_EntitySearch_{Guid.NewGuid():N}{suffix}";
        var group = Assert.IsType<EntityCreateResult>(await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(name)])));
        sc.TrackForCleanup(group.Guid);
        var request = new EntitySearchRequest(EntitySearchAction.SearchByName,
            Name: name, EntityType: [ScEntityType.UserGroup], NameSearchMode: EntityNameSearchMode.Is);

        var matching = Assert.IsType<string>(await EntitySearchTool.Execute(sc.Manager, request));
        Assert.Contains(group.Guid, IntegrationTestHelpers.ExtractGuids(matching));
        var missing = Assert.IsType<string>(await EntitySearchTool.Execute(sc.Manager,
            request with { Name = name + "&missing" }));
        Assert.DoesNotContain(group.Guid, IntegrationTestHelpers.ExtractGuids(missing));
    }

    [Fact]
    public async Task SearchByType_Camera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.SearchByType, EntityType: [ScEntityType.Camera]));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Theory]
    [InlineData(EntitySearchAction.SearchByType)]
    [InlineData(EntitySearchAction.SearchByName)]
    public async Task Search_AccessPointRule_UsesSdkAccessRuleType(EntitySearchAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var listedRules = Assert.IsType<string>(await AccessRuleListTool.Execute(sc.Manager,
            new AccessRuleListRequest(PageSize: 1)));
        var ruleId = Assert.Single(IntegrationTestHelpers.ExtractGuids(listedRules));

        using var fieldsDocument = JsonDocument.Parse(await sc.Manager.GetClient().GetEntityFields(
            ruleId.ToString(), "Guid", "Name", "EntityType"));
        var fields = fieldsDocument.RootElement;
        Assert.Equal(ruleId.ToString(), fields.GetProperty("Guid").GetString());
        Assert.Equal("AccessRule", fields.GetProperty("EntityType").GetString());
        var ruleName = fields.GetProperty("Name").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ruleName));

        var request = action switch
        {
            EntitySearchAction.SearchByType => new EntitySearchRequest(action,
                EntityType: [ScEntityType.AccessPointRule], PageSize: 100),
            EntitySearchAction.SearchByName => new EntitySearchRequest(action,
                EntityType: [ScEntityType.AccessPointRule], Name: ruleName,
                NameSearchMode: EntityNameSearchMode.Is, PageSize: 10),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        var result = Assert.IsType<string>(await EntitySearchTool.Execute(sc.Manager, request));
        Assert.Contains(ruleId, IntegrationTestHelpers.ExtractGuids(result));
    }

    [Fact]
    public async Task SearchByName_EmptyString_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.SearchByName, Name: ""));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task SearchByLogicalId_ValidType_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var (logicalId, name) = await FindCameraWithLogicalIdAsync();

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.SearchByLogicalId,
                EntityType: [ScEntityType.Camera], LogicalId: logicalId.ToString()));

        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        var entity = document.RootElement;
        Assert.Equal(name, entity.GetProperty("Name").GetString());
        Assert.Equal("Camera", entity.GetProperty("EntityType").GetString());
    }

    private async Task<(int LogicalId, string Name)> FindCameraWithLogicalIdAsync()
    {
        var json = await sc.Manager.GetClient().ListEntities(
            "Camera", pageSize: 100, page: 1, returnFields: ["Name"]);
        foreach (var cameraId in IntegrationTestHelpers.ExtractGuids(json))
        {
            var fieldsJson = await sc.Manager.GetClient().GetEntityFields(cameraId.ToString(), "Name", "LogicalId");
            using var fieldsDocument = JsonDocument.Parse(fieldsJson);
            var fields = fieldsDocument.RootElement;
            var name = fields.GetProperty("Name").GetString();
            var logicalId = ReadLogicalId(fields.GetProperty("LogicalId"));
            if (!string.IsNullOrWhiteSpace(name) && logicalId is > 0)
                return (logicalId.Value, name);
        }

        Assert.Fail("No camera with a positive LogicalId is configured for integration tests.");
        return default;
    }

    private static int? ReadLogicalId(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;
        return null;
    }

    [Fact]
    public async Task GetEntity_ValidId_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        // Use system configuration entity GUID which always exists
        var systemConfigGuid = new Guid("00000000-0000-0000-0000-000000000007");

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.GetEntity, Id: systemConfigGuid));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetFields_ValidIdAndFields_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var systemConfigGuid = new Guid("00000000-0000-0000-0000-000000000007");

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.GetFields,
                Id: systemConfigGuid, Fields: ["Name"]));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetFieldsBatch_ValidItems_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var systemConfigGuid = new Guid("00000000-0000-0000-0000-000000000007");

        var result = await EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.GetFieldsBatch,
                Items: [new EntityFieldsBatchItem(systemConfigGuid, ["Name"])]));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Theory]
    [MemberData(nameof(EntityTypes))]
    public async Task SearchByType_AllEntityTypes_ReachSecurityCenter(ScEntityType entityType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.SearchByType, EntityType: [entityType])));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    [Theory]
    [MemberData(nameof(NameSearchModes))]
    public async Task SearchByName_AllModes_ReachSecurityCenter(EntityNameSearchMode mode)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => EntitySearchTool.Execute(sc.Manager,
            new EntitySearchRequest(EntitySearchAction.SearchByName,
                Name: "MCP", NameSearchMode: mode)));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

}
