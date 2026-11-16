using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaSecurityToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(AreaSecurityAction.ModifyPeopleCount, "cardholder_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(AreaSecurityAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await CreateAreaAsync();
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Area", wrong => (action, parameter) switch
        {
            (AreaSecurityAction.ModifyPeopleCount, "cardholder_id") => AreaSecurityTool.Execute(sc.Manager, new AreaSecurityRequest(action, Id: id, CardholderId: wrong, InAccess: true)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<AreaSecurityAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(AreaSecurityAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(action, Id: id, CardholderId: id, InAccess: true, AccessPointId: id, Shunt: true)));
        Assert.Equal(action is AreaSecurityAction.ForgiveAntipassback or AreaSecurityAction.ShuntAccessPointDevice ? "access_point_id" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────
    // Note: arm/disarm/unlock operations require a live area with hardware.
    // GetPeopleCount and ResetPeopleCount are exercised against any existing area
    // without requiring hardware.

    [Fact]
    public async Task GetPeopleCount_ExistingArea_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var areaId = await CreateAreaAsync();

        var result = await AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(AreaSecurityAction.GetPeopleCount, Id: areaId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ResetPeopleCount_ExistingArea_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var areaId = await CreateAreaAsync();

        var result = await AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(AreaSecurityAction.ResetPeopleCount, Id: areaId));

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(AreaSecurityAction.Arm)]
    [InlineData(AreaSecurityAction.Disarm)]
    [InlineData(AreaSecurityAction.ArmPerimeter)]
    [InlineData(AreaSecurityAction.ArmZones)]
    [InlineData(AreaSecurityAction.DisarmZones)]
    [InlineData(AreaSecurityAction.UnlockPerimeterDoors)]
    public async Task AreaAction_ExistingArea_Succeeds(AreaSecurityAction action)
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var areaId = await CreateAreaAsync();
        var result = await AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(action, Id: areaId));

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModifyPeopleCount_ExistingAreaAndCardholder_Succeeds(bool inAccess)
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var areaId = await CreateAreaAsync();
        var cardholderId = await FindFirstEntityIdAsync("Cardholder");
        var result = await AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(AreaSecurityAction.ModifyPeopleCount,
                Id: areaId,
                CardholderId: cardholderId,
                InAccess: inAccess));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ForgiveAntipassback_ExistingAccessPoint_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var accessPointId = await FindFirstAccessPointIdAsync();
        var result = await AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(AreaSecurityAction.ForgiveAntipassback,
                AccessPointId: accessPointId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ShuntAccessPointDevice_UnknownAccessPoint_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => AreaSecurityTool.Execute(sc.Manager,
            new AreaSecurityRequest(AreaSecurityAction.ShuntAccessPointDevice,
                AccessPointId: Guid.NewGuid(), Shunt: true)));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }

    private async Task<Guid> CreateAreaAsync()
    {
        var created = (EntityCreateResult)await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_Area_{Guid.NewGuid():N}")]));
        Assert.NotEqual(default, created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private async Task<Guid> FindFirstAccessPointIdAsync()
    {
        var client = sc.Manager.GetClient();
        var doorsJson = await client.ListEntities("Door", pageSize: 100, page: 1, returnFields: ["Name"]);
        using var doorsDocument = JsonDocument.Parse(doorsJson);
        var doors = GetRows(doorsDocument.RootElement);

        foreach (var door in doors)
        {
            if (!door.TryGetProperty("Guid", out var guidProperty) ||
                !Guid.TryParse(guidProperty.GetString(), out var doorId))
                continue;

            var sidesJson = await client.GetEntityField(doorId.ToString(), "DoorSides");
            using var sidesDocument = JsonDocument.Parse(sidesJson);
            if (!IntegrationTestHelpers.GetResult(sidesDocument.RootElement).TryGetProperty("DoorSides", out var sides) ||
                sides.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var side in sides.EnumerateArray())
            {
                if (Guid.TryParse(side.GetString(), out var accessPointId))
                    return accessPointId;
            }
        }

        throw new InvalidOperationException("Integration test requires a door with an access point.");
    }

    private async Task<Guid> FindFirstEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType, pageSize: 1, page: 1);
        var result = IntegrationTestHelpers.ExtractFirstGuid(json);
        if (!result.HasValue)
            throw new InvalidOperationException($"Integration test requires at least one {entityType} entity.");
        return result.Value;
    }

    private static IReadOnlyList<JsonElement> GetRows(JsonElement root)
    {
        root = IntegrationTestHelpers.GetResult(root);
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToArray();
        if (root.TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array)
            return body.EnumerateArray().ToArray();
        return [];
    }
}
