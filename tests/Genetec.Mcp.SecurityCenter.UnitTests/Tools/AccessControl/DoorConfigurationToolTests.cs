using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class DoorConfigurationToolTests
{
    [Theory]
    [InlineData(DoorConfigAction.AddDoorSideAccessRule, "access_rule_id")]
    [InlineData(DoorConfigAction.RemoveDoorSideAccessRule, "access_rule_id")]
    [InlineData(DoorConfigAction.SetUnit, "unit_id")]
    [InlineData(DoorConfigAction.SetInterfaceModule, "interface_id")]
    public async Task WrongLinkedType_RejectsBeforeMutation(DoorConfigAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Door");
        handler.AddEntity(wrong, "UserGroup");
        handler.AddEntity(escort, "Cardholder");
        var request = (action, parameter) switch
        {
            (DoorConfigAction.AddDoorSideAccessRule, "access_rule_id") => new DoorConfigRequest(action, id, AccessRuleId: wrong, AccessRuleSide: DoorAccessRuleSide.Both),
            (DoorConfigAction.RemoveDoorSideAccessRule, "access_rule_id") => new DoorConfigRequest(action, id, AccessRuleId: wrong, AccessRuleSide: DoorAccessRuleSide.Both),
            (DoorConfigAction.SetUnit, "unit_id") => new DoorConfigRequest(action, id, UnitId: wrong),
            (DoorConfigAction.SetInterfaceModule, "interface_id") => new DoorConfigRequest(action, id, InterfaceId: wrong),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<DoorConfigAction>()
        .Where(action => action != DoorConfigAction.ActivityReport).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(DoorConfigAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new DoorConfigRequest(action, Id: id, DoorSideId: id, AccessPointId: id, AccessRuleId: id, AccessRuleSide: DoorAccessRuleSide.Both, Update: new DoorEntityUpdateRequest(Name: "blocked"), BuzzerState: true, EnableMaintenance: true, Unlock: true, StartTime: "2030-01-01T00:00:00Z", EndTime: "2030-01-01T01:00:00Z", CardholderId: id, CardholderGroupId: id, DeviceId: id, UnitId: id, InterfaceId: id)));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }



    [Fact]
    public async Task ActivityReport_WithoutTimeRange_PreservesEntityFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await DoorConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new DoorConfigRequest(DoorConfigAction.ActivityReport, id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"/WebSdk/report/DoorActivity?q=Doors@{id}",
            Uri.UnescapeDataString(request.AbsolutePath + request.Query));
    }

    public static IEnumerable<object[]> MissingRequiredParameters()
    {
        var id = Guid.NewGuid();
        var accessPoint = Guid.NewGuid();
        var doorSide = Guid.NewGuid();
        var rule = Guid.NewGuid();
        var cardholder = Guid.NewGuid();
        var group = Guid.NewGuid();

        yield return [new DoorConfigRequest(DoorConfigAction.GetAccessPoint, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetAccessPoint, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetAccessPoint, id, AccessPointId: accessPoint), "update"];
        yield return [new DoorConfigRequest(DoorConfigAction.GetDoorSide, id), "door_side_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetDoorSide, id), "door_side_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetDoorSide, id, DoorSideId: doorSide), "update"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule, id), "access_rule_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveDoorSideAccessRule, id), "access_rule_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule, id, AccessRuleId: rule), "access_rule_side"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveDoorSideAccessRule, id, AccessRuleId: rule), "access_rule_side"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholder, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholder, id, AccessPointId: accessPoint), "cardholder_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholder, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholder, id, AccessPointId: accessPoint), "cardholder_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholderGroup, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.AddAccessPointCardholderGroup, id, AccessPointId: accessPoint), "cardholder_group_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholderGroup, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.RemoveAccessPointCardholderGroup, id, AccessPointId: accessPoint), "cardholder_group_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetAccessPointDevice, id), "access_point_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetAccessPointDevice, id, AccessPointId: accessPoint), "device_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetUnit, id), "unit_id"];
        yield return [new DoorConfigRequest(DoorConfigAction.SetInterfaceModule, id), "interface_id"];
    }

    [Theory]
    [MemberData(nameof(MissingRequiredParameters))]
    public async Task ActionMissingRequiredParameter_ThrowsArgumentException(
        DoorConfigRequest request,
        string parameterName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains(parameterName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SetBuzzer_NullBuzzerState_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.SetBuzzer, Id: Guid.NewGuid(), BuzzerState: null)));

        Assert.Contains("buzzer_state", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetMaintenanceMode_NullEnableMaintenance_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.SetMaintenanceMode, Id: Guid.NewGuid(), EnableMaintenance: null)));

        Assert.Contains("enable_maintenance", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideLockSchedule_NullUnlock_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, Id: Guid.NewGuid(),
                    Unlock: null, StartTime: "2026-08-11T17:00:00Z", EndTime: "2026-08-11T18:00:00Z")));

        Assert.Contains("unlock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideLockSchedule_NullStartTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, Id: Guid.NewGuid(),
                    Unlock: true, StartTime: null, EndTime: "2026-08-11T18:00:00Z")));

        Assert.Contains("start_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideLockSchedule_NullEndTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, Id: Guid.NewGuid(),
                    Unlock: true, StartTime: "2026-08-11T17:00:00Z", EndTime: null)));

        Assert.Contains("end_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideLockSchedule_InvalidTimestamp_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, Id: Guid.NewGuid(),
                    Unlock: true, StartTime: "not-a-timestamp", EndTime: "2026-08-11T18:00:00Z")));

        Assert.Contains("ISO 8601", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideLockSchedule_EndBeforeStart_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.OverrideLockSchedule, Id: Guid.NewGuid(),
                    Unlock: true, StartTime: "2026-08-11T18:00:00Z", EndTime: "2026-08-11T17:00:00Z")));

        Assert.Contains("later", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddDoorSideAccessRule_NullAccessRuleId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule, Id: Guid.NewGuid(),
                    AccessRuleId: null, AccessRuleSide: DoorAccessRuleSide.Both)));

        Assert.Contains("access_rule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddDoorSideAccessRule_NullAccessRuleSide_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.AddDoorSideAccessRule, Id: Guid.NewGuid(),
                    AccessRuleId: Guid.NewGuid(), AccessRuleSide: null)));

        Assert.Contains("access_rule_side", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetUnit_NullUnitId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(),
                new DoorConfigRequest(DoorConfigAction.SetUnit, Id: Guid.NewGuid(), UnitId: null)));

        Assert.Contains("unit_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityReport_WithTimeRange_UsesTimeFilter()
    {
        var handler = new Tools.ToolTestHandler();
        var id = Guid.NewGuid();

        await DoorConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new DoorConfigRequest(DoorConfigAction.ActivityReport, id,
                TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"));

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        Assert.Contains(
            $"report/DoorActivity?q=Doors@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            uri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActivityReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(bool provideStart)
    {
        var request = new DoorConfigRequest(DoorConfigAction.ActivityReport, Guid.NewGuid(),
            TimeStart: provideStart ? "2024-01-01T00:00:00Z" : null,
            TimeEnd: provideStart ? null : "2024-01-02T00:00:00Z");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DoorConfigurationTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DoorConfigAction.GetAccessPoint, "access_point_id")]
    [InlineData(DoorConfigAction.SetAccessPoint, "access_point_id")]
    [InlineData(DoorConfigAction.GetDoorSide, "door_side_id")]
    [InlineData(DoorConfigAction.SetDoorSide, "door_side_id")]
    [InlineData(DoorConfigAction.AddAccessPointCardholder, "access_point_id")]
    [InlineData(DoorConfigAction.RemoveAccessPointCardholder, "access_point_id")]
    [InlineData(DoorConfigAction.AddAccessPointCardholderGroup, "access_point_id")]
    [InlineData(DoorConfigAction.RemoveAccessPointCardholderGroup, "access_point_id")]
    [InlineData(DoorConfigAction.SetAccessPointDevice, "access_point_id")]
    public async Task AccessPointActions_ForeignDoorAccessPoint_RejectBeforeMutation(
        DoorConfigAction action, string parameterName)
    {
        var doorId = Guid.NewGuid();
        var otherDoorId = Guid.NewGuid();
        var accessPointId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(doorId, "Door", doorSides: []);
        handler.AddEntity(accessPointId, "AccessPoint", doorId: otherDoorId);
        var request = CreateAccessPointRequest(action, doorId, accessPointId);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(parameterName, error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task GetDoorSide_AccessPointOwnedByDoorButNotListedAsSide_Rejects()
    {
        var doorId = Guid.NewGuid();
        var accessPointId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(doorId, "Door", doorSides: []);
        handler.AddEntity(accessPointId, "AccessPoint", doorId: doorId);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(
            TestHelpers.FakeManager(handler),
            new DoorConfigRequest(DoorConfigAction.GetDoorSide, doorId, DoorSideId: accessPointId)));

        Assert.Equal("door_side_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(DoorConfigAction.AddAccessPointCardholder, "cardholder_id")]
    [InlineData(DoorConfigAction.RemoveAccessPointCardholder, "cardholder_id")]
    [InlineData(DoorConfigAction.AddAccessPointCardholderGroup, "cardholder_group_id")]
    [InlineData(DoorConfigAction.RemoveAccessPointCardholderGroup, "cardholder_group_id")]
    [InlineData(DoorConfigAction.SetAccessPointDevice, "device_id")]
    public async Task AccessPointMembershipAndDeviceActions_WrongArgumentType_RejectBeforeMutation(
        DoorConfigAction action, string parameterName)
    {
        var doorId = Guid.NewGuid();
        var accessPointId = Guid.NewGuid();
        var wrongArgumentId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(doorId, "Door", doorSides: [accessPointId]);
        handler.AddEntity(accessPointId, "AccessPoint", doorId: doorId);
        handler.AddEntity(wrongArgumentId, "UserGroup");

        var request = action switch
        {
            DoorConfigAction.AddAccessPointCardholder or DoorConfigAction.RemoveAccessPointCardholder =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderId: wrongArgumentId),
            DoorConfigAction.AddAccessPointCardholderGroup or DoorConfigAction.RemoveAccessPointCardholderGroup =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderGroupId: wrongArgumentId),
            _ => new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, DeviceId: wrongArgumentId)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorConfigurationTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(parameterName, error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SetAccessPointDevice_EmptyGuid_ClearsDeviceWithoutEntityLookup()
    {
        var doorId = Guid.NewGuid();
        var accessPointId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(doorId, "Door", doorSides: [accessPointId]);
        handler.AddEntity(accessPointId, "AccessPoint", doorId: doorId);

        var result = await DoorConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new DoorConfigRequest(DoorConfigAction.SetAccessPointDevice, doorId,
                AccessPointId: accessPointId, DeviceId: Guid.Empty));

        Assert.IsType<string>(result);
        Assert.Equal(2, handler.Requests.Count(observed => observed.Method == HttpMethod.Get));
        var update = Assert.Single(handler.Requests, observed => observed.Method == HttpMethod.Post);
        Assert.Contains(Guid.Empty.ToString(), Uri.UnescapeDataString(update.Uri.ToString()), StringComparison.Ordinal);
    }

    private static DoorConfigRequest CreateAccessPointRequest(
        DoorConfigAction action, Guid doorId, Guid accessPointId)
    {
        var update = new DoorEntityUpdateRequest(Description: "Should not update");
        return action switch
        {
            DoorConfigAction.SetAccessPoint => new DoorConfigRequest(action, doorId,
                AccessPointId: accessPointId, Update: update),
            DoorConfigAction.SetDoorSide => new DoorConfigRequest(action, doorId,
                DoorSideId: accessPointId, Update: update),
            DoorConfigAction.GetDoorSide => new DoorConfigRequest(action, doorId, DoorSideId: accessPointId),
            DoorConfigAction.AddAccessPointCardholder or DoorConfigAction.RemoveAccessPointCardholder =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderId: Guid.NewGuid()),
            DoorConfigAction.AddAccessPointCardholderGroup or DoorConfigAction.RemoveAccessPointCardholderGroup =>
                new DoorConfigRequest(action, doorId, AccessPointId: accessPointId, CardholderGroupId: Guid.NewGuid()),
            DoorConfigAction.SetAccessPointDevice => new DoorConfigRequest(action, doorId,
                AccessPointId: accessPointId, DeviceId: Guid.NewGuid()),
            _ => new DoorConfigRequest(action, doorId, AccessPointId: accessPointId)
        };
    }

}
