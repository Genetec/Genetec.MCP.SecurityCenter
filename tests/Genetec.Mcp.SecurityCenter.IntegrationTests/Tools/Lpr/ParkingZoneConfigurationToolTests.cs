using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class ParkingZoneConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task GetCameras_UsesGetLprCameraInformationForAnLprDevice()
    {
        RequireSecurityCenter();
        var zoneId = await GetFirstEntityIdAsync("ParkingZone");
        var deviceId = await GetFirstLprDeviceIdAsync();

        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.GetCameras,
                CameraId: deviceId));

        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("GetLprCameraInformation", out var information));
        Assert.Equal(JsonValueKind.Array, information.ValueKind);
    }

    [Fact]
    public async Task AddLprCamera_ThenGetCameraInformation_PersistsDeviceConfiguration()
    {
        RequireSecurityCenter();
        var zoneId = await CreateTestZoneAsync();
        var deviceId = await GetFirstLprDeviceIdAsync();

        var addResult = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.AddLprCamera,
                CameraId: deviceId,
                Position: ParkingZoneLprCameraPosition.Entrance,
                RelativeMotion: ParkingZoneLprRelativeMotion.Ignored));

        var addedAccessPointId = AssertGuidMethodResult(addResult, "AddLprCamera");

        var zoneJson = await sc.Manager.GetClient().GetEntityTrimmed(zoneId.ToString());
        Assert.True(TryGetCameraAccessPoint(zoneJson, deviceId, out _),
            "The added device was not present in the parking zone LprCamerasInfo property.");

        var readResult = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.GetCameras,
                CameraId: deviceId));

        var readJson = Assert.IsType<string>(readResult);
        using var document = IntegrationTestHelpers.ParseJson(readJson);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("GetLprCameraInformation", out var information));
        Assert.Contains(information.EnumerateArray(), item =>
            item.TryGetProperty("DeviceId", out var id) &&
            Guid.TryParse(id.GetString(), out var actual) && actual == deviceId);

        Assert.True(TryGetCameraAccessPoint(zoneJson, deviceId, out var accessPointId));
        Assert.Equal(addedAccessPointId, accessPointId);
        AssertBooleanMethodResult(await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.RemoveLprCamera,
                CameraId: addedAccessPointId)), "RemoveLprCamera");
    }

    [Fact]
    public async Task RemoveLprCamera_RemovesTheCreatedAccessPoint()
    {
        RequireSecurityCenter();
        var zoneId = await CreateTestZoneAsync();
        var deviceId = await GetFirstLprDeviceIdAsync();

        var addResult = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.AddLprCamera,
                CameraId: deviceId,
                Position: ParkingZoneLprCameraPosition.Exit,
                RelativeMotion: ParkingZoneLprRelativeMotion.MovingAway));
        var addedAccessPointId = AssertGuidMethodResult(addResult, "AddLprCamera");

        var configuredZoneJson = await sc.Manager.GetClient().GetEntityTrimmed(zoneId.ToString());
        Assert.True(TryGetCameraAccessPoint(configuredZoneJson, deviceId, out var accessPointId),
            "The LPR camera access point was not created.");
        Assert.Equal(addedAccessPointId, accessPointId);

        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.RemoveLprCamera,
                CameraId: accessPointId));

        AssertBooleanMethodResult(result, "RemoveLprCamera");

        var removedZoneJson = await sc.Manager.GetClient().GetEntityTrimmed(zoneId.ToString());
        Assert.False(TryGetCameraAccessPoint(removedZoneJson, deviceId, out _),
            "The LPR camera remained in the parking zone after remove_lpr_camera.");
    }

    [Fact]
    public async Task AddRule_ThenReadZone_ContainsRuleInParkingZoneRules()
    {
        RequireSecurityCenter();
        var zoneId = await CreateTestZoneAsync();
        var ruleId = await GetFirstEntityIdAsync("ParkingRule");

        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.AddRule,
                RuleId: ruleId));

        AssertBooleanMethodResult(result, "AddParkingZoneRule");

        var zoneJson = await sc.Manager.GetClient().GetEntityTrimmed(zoneId.ToString());
        Assert.True(ContainsGuidInArray(zoneJson, "ParkingZoneRules", ruleId),
            "The parking rule was not present in the documented ParkingZoneRules property.");
    }

    [Fact]
    public async Task RemoveRule_AfterAddingRule_RemovesRuleFromParkingZoneRules()
    {
        RequireSecurityCenter();
        var zoneId = await CreateTestZoneAsync();
        var ruleId = await GetFirstEntityIdAsync("ParkingRule");

        var addResult = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.AddRule,
                RuleId: ruleId));
        AssertBooleanMethodResult(addResult, "AddParkingZoneRule");

        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(
                zoneId,
                ParkingZoneConfigurationAction.RemoveRule,
                RuleId: ruleId));

        AssertBooleanMethodResult(result, "RemoveParkingZoneRule");

        var zoneJson = await sc.Manager.GetClient().GetEntityTrimmed(zoneId.ToString());
        Assert.False(ContainsGuidInArray(zoneJson, "ParkingZoneRules", ruleId),
            "The parking rule was still present after remove_rule.");
    }

    [Fact]
    public async Task GetCapacity_ValidZone_ReturnsOccupancy()
    {
        RequireSecurityCenter();
        var zoneId = await GetFirstEntityIdAsync("ParkingZone");

        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(zoneId, ParkingZoneConfigurationAction.GetCapacity));

        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("Occupancy", out var occupancy),
            "The documented Occupancy field was not returned.");
        Assert.Equal(JsonValueKind.Object, occupancy.ValueKind);
    }

    [Fact]
    public async Task AddLprCamera_ApproachingMotion_ReachesSecurityCenter()
    {
        RequireSecurityCenter();
        var zoneId = await CreateTestZoneAsync();
        var deviceId = await GetFirstLprDeviceIdAsync();
        var result = await ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(zoneId,
                ParkingZoneConfigurationAction.AddLprCamera,
                CameraId: deviceId,
                Position: ParkingZoneLprCameraPosition.Entrance,
                RelativeMotion: ParkingZoneLprRelativeMotion.Approaching));

        Assert.NotNull(result);
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private async Task<Guid> GetFirstEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType);
        var id = IntegrationTestHelpers.ExtractFirstGuid(json);
        Assert.True(id.HasValue, $"No {entityType} entities are configured for integration tests.");
        return id.Value;
    }

    private async Task<Guid> GetFirstLprDeviceIdAsync()
    {
        var unitsJson = await sc.Manager.GetClient().ListEntities("LprUnit");
        using var unitsDocument = IntegrationTestHelpers.ParseJson(unitsJson);
        var units = IntegrationTestHelpers.GetResult(unitsDocument.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(unitsDocument.RootElement).EnumerateArray()
            : IntegrationTestHelpers.GetResult(unitsDocument.RootElement).TryGetProperty("Body", out var body)
                ? body.EnumerateArray()
                : [];

        foreach (var unit in units)
        {
            var unitIdValue = unit.TryGetProperty("Guid", out var guidProperty)
                ? guidProperty.GetString()
                : null;
            if (!Guid.TryParse(unitIdValue, out var unitId))
                continue;

            var unitJson = await sc.Manager.GetClient().GetEntityTrimmed(unitId.ToString());
            using var unitDocument = IntegrationTestHelpers.ParseJson(unitJson);
            if (!IntegrationTestHelpers.GetResult(unitDocument.RootElement).TryGetProperty("Devices", out var devices) ||
                devices.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var device in devices.EnumerateArray())
            {
                if (!Guid.TryParse(device.GetString(), out var deviceId))
                    continue;

                var deviceJson = await sc.Manager.GetClient().GetEntityTrimmed(deviceId.ToString());
                using var deviceDocument = IntegrationTestHelpers.ParseJson(deviceJson);
                if (IntegrationTestHelpers.GetResult(deviceDocument.RootElement).TryGetProperty("DeviceType", out var deviceType) &&
                    deviceType.ValueKind == JsonValueKind.String &&
                    string.Equals(deviceType.GetString(), "Lpr", StringComparison.OrdinalIgnoreCase))
                    return deviceId;
            }
        }

        Assert.Fail("No usable LPR camera device is configured for integration tests.");
        return default;
    }

    private async Task<Guid> CreateTestZoneAsync()
    {
        var name = $"MCP_ParkingZoneConfiguration_{Guid.NewGuid():N}";
        var result = await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default, created.Guid);
        sc.TrackForCleanup(created.Guid);
        return created.Guid;
    }

    private static void AssertBooleanMethodResult(object result, string methodName)
    {
        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty(methodName, out var value),
            $"The {methodName} result was not returned.");
        Assert.Equal(JsonValueKind.True, value.ValueKind);
    }

    private static Guid AssertGuidMethodResult(object result, string methodName)
    {
        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty(methodName, out var value),
            $"The {methodName} result was not returned.");
        var valueText = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        Assert.True(Guid.TryParse(valueText, out var id) && id != Guid.Empty,
            $"The {methodName} result did not contain a non-empty GUID.");
        return id;
    }

    private static bool TryGetCameraAccessPoint(string json, Guid deviceId, out Guid accessPointId)
    {
        accessPointId = default;
        using var document = IntegrationTestHelpers.ParseJson(json);
        if (!IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("LprCamerasInfo", out var cameras) ||
            cameras.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var camera in cameras.EnumerateArray())
        {
            if (!camera.TryGetProperty("DeviceId", out var device) ||
                !Guid.TryParse(device.GetString(), out var actualDevice) ||
                actualDevice != deviceId ||
                !camera.TryGetProperty("AccessPointId", out var accessPoint) ||
                !Guid.TryParse(accessPoint.GetString(), out accessPointId))
                continue;

            return true;
        }

        return false;
    }

    private static bool ContainsGuidInArray(string json, string propertyName, Guid expected)
    {
        using var document = IntegrationTestHelpers.ParseJson(json);
        if (!IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return false;

        return values.EnumerateArray().Any(value =>
            value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out var actual) &&
            actual == expected);
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.GetCameras)]
    [InlineData(ParkingZoneConfigurationAction.AddLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.RemoveLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.AddRule)]
    [InlineData(ParkingZoneConfigurationAction.RemoveRule)]
    [InlineData(ParkingZoneConfigurationAction.GetCapacity)]
    public async Task TargetActions_WrongEntityType_RejectBeforeOperation(ParkingZoneConfigurationAction action)
    {
        var (linkedId, linkedName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = linkedId;
        var name = linkedName;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(id, action, CameraId: linkedId, RuleId: linkedId,
                Position: ParkingZoneLprCameraPosition.Entrance, RelativeMotion: ParkingZoneLprRelativeMotion.Ignored)));
        Assert.Equal("zone_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, linkedId, "UserGroup", linkedName);
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.GetCameras)]
    [InlineData(ParkingZoneConfigurationAction.AddLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.RemoveLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.AddRule)]
    [InlineData(ParkingZoneConfigurationAction.RemoveRule)]
    public async Task LinkedActions_WrongEntityType_RejectBeforeMutation(ParkingZoneConfigurationAction action)
    {
        var (linkedId, linkedName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_ParkingZone_Type_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await ParkingZoneTool.Execute(sc.Manager,
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneConfigurationTool.Execute(sc.Manager,
            new ParkingZoneConfigurationRequest(id, action, CameraId: linkedId, RuleId: linkedId,
                Position: ParkingZoneLprCameraPosition.Entrance, RelativeMotion: ParkingZoneLprRelativeMotion.Ignored)));
        Assert.Equal(action is ParkingZoneConfigurationAction.AddRule or ParkingZoneConfigurationAction.RemoveRule ? "rule_id" : "camera_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "ParkingZone", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, linkedId, "UserGroup", linkedName);
    }
}
