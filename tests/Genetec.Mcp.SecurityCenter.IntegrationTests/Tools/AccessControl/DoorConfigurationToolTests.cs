using System.Globalization;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(DoorConfigAction.AddDoorSideAccessRule, "access_rule_id")]
    [InlineData(DoorConfigAction.RemoveDoorSideAccessRule, "access_rule_id")]
    [InlineData(DoorConfigAction.SetUnit, "unit_id")]
    [InlineData(DoorConfigAction.SetInterfaceModule, "interface_id")]
    public async Task WrongLinkedType_RejectsAndPreservesPrimary(DoorConfigAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = Assert.IsType<EntityCreateResult>(await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Linked_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var id = created.Guid;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, id, "Door", wrong => (action, parameter) switch
        {
            (DoorConfigAction.AddDoorSideAccessRule, "access_rule_id") => DoorConfigurationTool.Execute(sc.Manager, new DoorConfigRequest(action, id, AccessRuleId: wrong, AccessRuleSide: DoorAccessRuleSide.Both)),
            (DoorConfigAction.RemoveDoorSideAccessRule, "access_rule_id") => DoorConfigurationTool.Execute(sc.Manager, new DoorConfigRequest(action, id, AccessRuleId: wrong, AccessRuleSide: DoorAccessRuleSide.Both)),
            (DoorConfigAction.SetUnit, "unit_id") => DoorConfigurationTool.Execute(sc.Manager, new DoorConfigRequest(action, id, UnitId: wrong)),
            (DoorConfigAction.SetInterfaceModule, "interface_id") => DoorConfigurationTool.Execute(sc.Manager, new DoorConfigRequest(action, id, InterfaceId: wrong)),
            _ => throw new InvalidOperationException()
        }, parameter);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<DoorConfigAction>()
        .Where(action => action != DoorConfigAction.ActivityReport).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(DoorConfigAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(action, Id: id, DoorSideId: id, AccessPointId: id, AccessRuleId: id, AccessRuleSide: DoorAccessRuleSide.Both, Update: new DoorEntityUpdateRequest(Name: "blocked"), BuzzerState: true, EnableMaintenance: true, Unlock: true, StartTime: "2030-01-01T00:00:00Z", EndTime: "2030-01-01T01:00:00Z", CardholderId: id, CardholderGroupId: id, DeviceId: id, UnitId: id, InterfaceId: id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    public static IEnumerable<object[]> AccessRuleSides()
    {
        yield return [DoorAccessRuleSide.Default];
        yield return [DoorAccessRuleSide.Both];
        yield return [DoorAccessRuleSide.In];
        yield return [DoorAccessRuleSide.Out];
    }

    [Theory]
    [InlineData(DoorConfigAction.Unlock)]
    [InlineData(DoorConfigAction.OpenDoor)]
    [InlineData(DoorConfigAction.IsOpen)]
    [InlineData(DoorConfigAction.IsLocked)]
    [InlineData(DoorConfigAction.GetDoorSides)]
    [InlineData(DoorConfigAction.ActivityReport)]
    public async Task SimpleAction_ExistingDoor_Succeeds(DoorConfigAction action)
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var target = await FindDoorWithSidesAsync();

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(action, target.DoorId));

        Assert.NotNull(result);
    }

    [Theory]
    [MemberData(nameof(AccessRuleSides))]
    public async Task AddAndRemoveAccessRule_AllSides_Succeeds(DoorAccessRuleSide side)
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var target = await FindDoorWithSidesAsync();
        var createResult = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem($"MCP_DoorConfigRule_{Guid.NewGuid():N}")]));
        var createdRule = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(createdRule.Guid);

        var addResult = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule,
                target.DoorId, AccessRuleId: createdRule.Guid, AccessRuleSide: side));
        var removeResult = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.RemoveDoorSideAccessRule,
                target.DoorId, AccessRuleId: createdRule.Guid, AccessRuleSide: side));

        Assert.NotNull(addResult);
        Assert.NotNull(removeResult);
    }

    [Fact]
    public async Task FullDoorConfigurationWorkflow_AllActionsAndSides_Succeed()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var target = await FindDoorWithSidesAsync();
        var cardholderId = await FindFirstEntityIdAsync("Cardholder");
        var cardholderGroupId = await FindFirstEntityIdAsync("CardholderGroup");
        var createResult = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem($"MCP_DoorConfigRule_{Guid.NewGuid():N}")]));
        var accessRule = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(accessRule.Guid);

        var zeroGuid = Guid.Empty;
        var start = DateTimeOffset.UtcNow.AddSeconds(-5).ToString("O");
        var end = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O");

        try
        {
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.Unlock, target.DoorId)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.OpenDoor, target.DoorId)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetBuzzer, target.DoorId, BuzzerState: true)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetBuzzer, target.DoorId, BuzzerState: false)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, target.DoorId,
                    Unlock: true, StartTime: start, EndTime: end)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, target.DoorId,
                    Unlock: false, StartTime: start, EndTime: end)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode, target.DoorId,
                    EnableMaintenance: true)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode, target.DoorId,
                    EnableMaintenance: false)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.IsOpen, target.DoorId)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.IsLocked, target.DoorId)));

            foreach (var side in target.Sides)
            {
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.GetAccessPoint, target.DoorId,
                        AccessPointId: side)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.GetDoorSide, target.DoorId,
                        DoorSideId: side)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetAccessPoint, target.DoorId,
                        AccessPointId: side,
                        Update: new DoorEntityUpdateRequest(Description: "MCP full workflow"))));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetDoorSide, target.DoorId,
                        DoorSideId: side,
                        Update: new DoorEntityUpdateRequest(Description: "MCP full workflow"))));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetAccessPointDevice, target.DoorId,
                        AccessPointId: side, DeviceId: zeroGuid)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholder, target.DoorId,
                        AccessPointId: side, CardholderId: cardholderId)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholder, target.DoorId,
                        AccessPointId: side, CardholderId: cardholderId)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholderGroup, target.DoorId,
                        AccessPointId: side, CardholderGroupId: cardholderGroupId)));
                Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholderGroup, target.DoorId,
                        AccessPointId: side, CardholderGroupId: cardholderGroupId)));
            }

            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule, target.DoorId,
                    AccessRuleId: accessRule.Guid, AccessRuleSide: DoorAccessRuleSide.Default)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.RemoveDoorSideAccessRule, target.DoorId,
                    AccessRuleId: accessRule.Guid, AccessRuleSide: DoorAccessRuleSide.Default)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetUnit, target.DoorId, UnitId: zeroGuid)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetInterfaceModule, target.DoorId,
                    InterfaceId: zeroGuid)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.GetDoorSides, target.DoorId)));
            Assert.NotNull(await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.ActivityReport, target.DoorId)));
        }
        finally
        {
            foreach (var side in target.Sides)
            {
                await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetAccessPoint, target.DoorId,
                        AccessPointId: side, Update: new DoorEntityUpdateRequest(Description: "")));
                await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetDoorSide, target.DoorId,
                        DoorSideId: side, Update: new DoorEntityUpdateRequest(Description: "")));
                await DoorConfigurationTool.Execute(sc.Manager,
                    new DoorConfigRequest(DoorConfigAction.SetAccessPointDevice, target.DoorId,
                        AccessPointId: side, DeviceId: zeroGuid));
            }

            await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode, target.DoorId,
                    EnableMaintenance: false));
            await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetBuzzer, target.DoorId, BuzzerState: false));
        }
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task SetMaintenanceMode_ExistingDoor_ChangesState()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        try
        {
            await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode,
                    Id: created.Guid, EnableMaintenance: true));

            var json = await sc.Manager.GetClient().GetEntity(created.Guid.ToString());
            using var doc = JsonDocument.Parse(json);
            Assert.True(IntegrationTestHelpers.GetResult(doc.RootElement).GetProperty("MaintenanceModeActive").GetBoolean());
        }
        finally
        {
            await DoorConfigurationTool.Execute(sc.Manager,
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode,
                    Id: created.Guid, EnableMaintenance: false));
        }
    }

    [Fact]
    public async Task IsOpen_ExistingDoor_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_Door_{Guid.NewGuid():N}";
        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.IsOpen, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task IsLocked_ExistingDoor_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_Door_{Guid.NewGuid():N}";
        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.IsLocked, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDoorSides_ExistingDoor_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_Door_{Guid.NewGuid():N}";
        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.GetDoorSides, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AccessPointActions_RejectWrongTypeForeignOwnerAndWrongMemberTypes()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var door = await FindDoorWithSidesAsync();
        var otherDoor = await FindDoorWithSidesAsync(door.DoorId);
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var foreignSide = otherDoor.Sides[0];
        var beforeJson = await sc.Manager.GetClient().GetEntity(foreignSide.ToString());
        using var beforeDocument = IntegrationTestHelpers.ParseJson(beforeJson);
        var before = IntegrationTestHelpers.GetResult(beforeDocument.RootElement);
        var beforeDescription = before.TryGetProperty("Description", out var initialDescription)
            ? initialDescription.GetString()
            : null;

        foreach (var action in AccessPointActions())
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(sc.Manager,
                CreateAccessPointActionRequest(action, door.DoorId, wrongTarget.Id, wrongTarget.Id)));
            Assert.Equal(action is DoorConfigAction.GetDoorSide or DoorConfigAction.SetDoorSide
                ? "door_side_id"
                : "access_point_id", error.ParamName);
        }

        foreach (var action in AccessPointActions())
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(sc.Manager,
                CreateAccessPointActionRequest(action, door.DoorId, foreignSide, wrongTarget.Id)));
            Assert.Equal(action is DoorConfigAction.GetDoorSide or DoorConfigAction.SetDoorSide
                ? "door_side_id"
                : "access_point_id", error.ParamName);
        }

        foreach (var (action, argumentName) in new[]
                 {
                     (DoorConfigAction.AddAccessPointCardholder, "cardholder_id"),
                     (DoorConfigAction.RemoveAccessPointCardholder, "cardholder_id"),
                     (DoorConfigAction.AddAccessPointCardholderGroup, "cardholder_group_id"),
                     (DoorConfigAction.RemoveAccessPointCardholderGroup, "cardholder_group_id"),
                     (DoorConfigAction.SetAccessPointDevice, "device_id")
                 })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(sc.Manager,
                CreateAccessPointActionRequest(action, door.DoorId, door.Sides[0], wrongTarget.Id)));
            Assert.Equal(argumentName, error.ParamName);
        }

        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
        var foreignAccessPointJson = await sc.Manager.GetClient().GetEntity(foreignSide.ToString());
        using var foreignDocument = IntegrationTestHelpers.ParseJson(foreignAccessPointJson);
        var foreignAccessPoint = IntegrationTestHelpers.GetResult(foreignDocument.RootElement);
        Assert.Equal("AccessPoint", foreignAccessPoint.GetProperty("EntityType").GetString());
        Assert.Equal(otherDoor.DoorId, foreignAccessPoint.GetProperty("Door").GetGuid());
        Assert.Equal(beforeDescription, foreignAccessPoint.TryGetProperty("Description", out var description)
            ? description.GetString()
            : null);
    }

    [Fact]
    public async Task ActivityReport_ExistingDoor_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var name = $"MCP_Door_{Guid.NewGuid():N}";
        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.ActivityReport, Id: created.Guid));

        Assert.NotNull(result);

        var timeStart = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var timeEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var boundedResult = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.ActivityReport, Id: created.Guid,
                TimeStart: timeStart, TimeEnd: timeEnd));

        Assert.NotNull(boundedResult);
    }

    [Fact]
    public async Task Unlock_ExistingDoor_CompletesAction()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.Unlock, Id: created.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetBuzzer_ExistingDoor_TogglesState()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var onResult = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.SetBuzzer,
                Id: created.Guid, BuzzerState: true));
        var offResult = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.SetBuzzer,
                Id: created.Guid, BuzzerState: false));

        Assert.NotNull(onResult);
        Assert.NotNull(offResult);
    }

    [Fact]
    public async Task OverrideLockSchedule_ExistingDoor_CompletesAction()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        // DateTimeOffset emits +00:00; the tool must normalize it before putting
        // the action into the Web SDK query string (where '+' is decoded as a space).
        var start = DateTimeOffset.UtcNow.AddSeconds(-5).ToString("O");
        var end = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O");
        var result = await DoorConfigurationTool.Execute(sc.Manager,
            new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule,
                Id: created.Guid, Unlock: true, StartTime: start, EndTime: end));

        Assert.NotNull(result);
    }

    private async Task<DoorTarget> FindDoorWithSidesAsync(Guid? excludedDoorId = null)
    {
        var client = sc.Manager.GetClient();
        var json = await client.ListEntities("Door", pageSize: 100, page: 1, returnFields: ["Name"]);
        using var document = JsonDocument.Parse(json);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(document.RootElement).EnumerateArray().ToArray()
            : IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array
                ? body.EnumerateArray().ToArray()
                : [];

        foreach (var row in rows)
        {
            if (!row.TryGetProperty("Guid", out var guidProperty) ||
                !Guid.TryParse(guidProperty.GetString(), out var doorId))
                continue;
            if (doorId == excludedDoorId)
                continue;

            var sidesJson = await client.GetEntityField(doorId.ToString(), "DoorSides");
            using var sidesDocument = JsonDocument.Parse(sidesJson);
            if (!IntegrationTestHelpers.GetResult(sidesDocument.RootElement).TryGetProperty("DoorSides", out var sides) ||
                sides.ValueKind != JsonValueKind.Array || sides.GetArrayLength() < (excludedDoorId.HasValue ? 1 : 2))
                continue;

            var sideIds = sides.EnumerateArray()
                .Select(value => Guid.Parse(value.GetString()!))
                .Take(2)
                .ToArray();
            return new DoorTarget(doorId, sideIds);
        }

        throw new InvalidOperationException("Integration test requires a door with two configured door sides.");
    }

    private async Task<Guid> FindFirstEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType, pageSize: 1, page: 1);
        using var document = JsonDocument.Parse(json);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(document.RootElement).EnumerateArray().ToArray()
            : IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array
                ? body.EnumerateArray().ToArray()
                : [];

        var guid = rows.FirstOrDefault().GetProperty("Guid").GetString();
        if (!Guid.TryParse(guid, out var result))
            throw new InvalidOperationException($"Integration test requires at least one {entityType} entity.");
        return result;
    }

    private static IEnumerable<DoorConfigAction> AccessPointActions()
    {
        yield return DoorConfigAction.GetAccessPoint;
        yield return DoorConfigAction.SetAccessPoint;
        yield return DoorConfigAction.GetDoorSide;
        yield return DoorConfigAction.SetDoorSide;
        yield return DoorConfigAction.AddAccessPointCardholder;
        yield return DoorConfigAction.RemoveAccessPointCardholder;
        yield return DoorConfigAction.AddAccessPointCardholderGroup;
        yield return DoorConfigAction.RemoveAccessPointCardholderGroup;
        yield return DoorConfigAction.SetAccessPointDevice;
    }

    private static DoorConfigRequest CreateAccessPointActionRequest(
        DoorConfigAction action, Guid doorId, Guid accessPointId, Guid argumentId)
    {
        var update = new DoorEntityUpdateRequest(Description: "Must not update");
        return action switch
        {
            DoorConfigAction.SetAccessPoint => new DoorConfigRequest(action, doorId,
                AccessPointId: accessPointId, Update: update),
            DoorConfigAction.GetDoorSide => new DoorConfigRequest(action, doorId, DoorSideId: accessPointId),
            DoorConfigAction.SetDoorSide => new DoorConfigRequest(action, doorId,
                DoorSideId: accessPointId, Update: update),
            DoorConfigAction.AddAccessPointCardholder or DoorConfigAction.RemoveAccessPointCardholder =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderId: argumentId),
            DoorConfigAction.AddAccessPointCardholderGroup or DoorConfigAction.RemoveAccessPointCardholderGroup =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderGroupId: argumentId),
            DoorConfigAction.SetAccessPointDevice => new DoorConfigRequest(action, doorId,
                AccessPointId: accessPointId, DeviceId: argumentId),
            _ => new DoorConfigRequest(action, doorId, AccessPointId: accessPointId)
        };
    }

    private sealed record DoorTarget(Guid DoorId, IReadOnlyList<Guid> Sides);
}
