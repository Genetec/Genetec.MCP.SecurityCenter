using System.Globalization;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class ReaderConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task EnableCardAndPin_WrongScheduleType_RejectsAndPreservesReaderSettings()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = await GetReaderBackedAccessPointIdAsync(requireRestorableSettings: true);
        Assert.True(id.HasValue, "Linked schedule validation requires a reader-backed access point with readable stored settings.");
        var before = await ReadReaderSettingsAsync(id.Value);
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(id.Value, ReaderConfigAction.EnableCardAndPin, ActivationScheduleId: wrong.Id, FailureTimeout: TimeSpan.FromSeconds(10))));
        Assert.Equal("activation_schedule_id", error.ParamName);
        Assert.Equal(before, await ReadReaderSettingsAsync(id.Value));
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<ReaderConfigAction>()
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(ReaderConfigAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(id, action, AssuranceProfileId: id, ActivationScheduleId: id, FailureTimeout: TimeSpan.FromSeconds(10))));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ValidReader_ReturnsEntity()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");
        var readerId = await GetReaderBackedAccessPointIdAsync();
        Assert.True(readerId.HasValue,
            "Reader configuration integration requires a card-reader access point with an associated reader device in the first 50 access points.");

        var result = await ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(Id: readerId!.Value, Action: ReaderConfigAction.Get));

        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        var entity = IntegrationTestHelpers.GetResult(document.RootElement);
        var deviceId = await sc.Manager.GetClient().ResolveReaderDeviceId(readerId.Value);
        Assert.Equal(deviceId, entity.GetProperty("Guid").GetGuid());
        Assert.Equal("Device", entity.GetProperty("EntityType").GetString());
        Assert.Equal("Reader", entity.GetProperty("DeviceType").GetString());

        var directResult = await ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(deviceId, ReaderConfigAction.Get));
        using var directDocument = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(directResult));
        Assert.Equal(deviceId, IntegrationTestHelpers.GetResult(directDocument.RootElement).GetProperty("Guid").GetGuid());
    }

    [Fact]
    public async Task CardAndPinActions_ReadBackSettingsAndRestoreOriginalMode()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var readerId = await GetReaderBackedAccessPointIdAsync(requireRestorableSettings: true);
        Assert.True(readerId.HasValue,
            "Reader mode integration requires a backed card-reader access point with card_and_pin or card_or_pin mode, a stored activation schedule, and a supported timeout in the first 50 access points.");

        var original = await ReadReaderSettingsAsync(readerId.Value);
        try
        {
            await ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: original.ScheduleId, FailureTimeout: original.Timeout));
            Assert.Equal(original with { Mode = "CardAndPin" }, await ReadReaderSettingsAsync(readerId.Value));

            await ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableCardOrPin));
            Assert.Equal(original with { Mode = "CardOrPin" }, await ReadReaderSettingsAsync(readerId.Value));

            await ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: original.ScheduleId, FailureTimeout: original.Timeout));
            Assert.Equal(original with { Mode = "CardAndPin" }, await ReadReaderSettingsAsync(readerId.Value));

            await ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(readerId.Value, ReaderConfigAction.DisableCardAndPin));
            Assert.Equal(original with { Mode = "CardOrPin" }, await ReadReaderSettingsAsync(readerId.Value));
        }
        finally
        {
            await ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: original.ScheduleId, FailureTimeout: original.Timeout));
            if (original.Mode == "CardOrPin")
                await ReaderConfigurationTool.Execute(sc.Manager,
                    new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableCardOrPin));
            Assert.Equal(original, await ReadReaderSettingsAsync(readerId.Value));
        }
    }

    [Fact]
    public async Task EnableAssuranceProfile_ConfiguredProfile_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var assuranceProfileId = await GetFirstAssuranceProfileIdAsync();
        Assert.True(assuranceProfileId.HasValue,
            "Reader configuration integration requires a configured credential assurance profile in System Configuration.");

        var readerId = await GetReaderBackedAccessPointIdAsync();
        Assert.True(readerId.HasValue,
            "Reader configuration integration requires a card-reader access point with an associated reader device in the first 50 access points.");

        Assert.NotNull(await ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(readerId.Value, ReaderConfigAction.EnableAssuranceProfile,
                AssuranceProfileId: assuranceProfileId)));
    }

    [Fact]
    public async Task UnbackedAccessPoint_ReturnsActionableError()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var accessPointId = await GetUnbackedAccessPointIdAsync();
        Assert.True(accessPointId.HasValue,
            "Reader configuration integration requires an unbacked card-reader access point in the first 50 access points.");

        var scheduleId = await GetEntityIdByNameAsync("Schedule", "Always");
        Assert.True(scheduleId.HasValue, "Reader configuration integration requires a Schedule named 'Always'.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReaderConfigurationTool.Execute(sc.Manager,
                new ReaderConfigRequest(accessPointId.Value, ReaderConfigAction.EnableCardAndPin,
                    ActivationScheduleId: scheduleId, FailureTimeout: TimeSpan.FromSeconds(10))));

        Assert.Contains("no associated reader device", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid?> GetReaderBackedAccessPointIdAsync(bool requireRestorableSettings = false)
    {
        var json = await sc.Manager.GetClient().ListEntities("AccessPoint", pageSize: 50, page: 1);
        foreach (var id in IntegrationTestHelpers.ExtractGuids(json))
        {
            var entity = await sc.Manager.GetClient().GetEntity(id.ToString());
            using var document = JsonDocument.Parse(entity);
            var root = document.RootElement;
            if (!IsCardReader(root) || !root.TryGetProperty("Device", out var device))
                continue;
            if (device.ValueKind == JsonValueKind.String &&
                Guid.TryParse(device.GetString(), out var deviceId) && deviceId != Guid.Empty)
            {
                if (requireRestorableSettings)
                {
                    var settings = await ReadReaderSettingsAsync(id);
                    if (settings.Mode is not ("CardAndPin" or "CardOrPin") || settings.ScheduleId == Guid.Empty ||
                        settings.Timeout < TimeSpan.Zero || settings.Timeout > TimeSpan.FromSeconds(999))
                        continue;
                }
                return id;
            }
        }

        return null;
    }

    private async Task<ReaderSettingsSnapshot> ReadReaderSettingsAsync(Guid accessPointId)
    {
        var json = Assert.IsType<string>(await ReaderConfigurationTool.Execute(sc.Manager,
            new ReaderConfigRequest(accessPointId, ReaderConfigAction.Get)));
        using var document = IntegrationTestHelpers.ParseJson(json);
        var reader = IntegrationTestHelpers.GetResult(document.RootElement);
        var deviceId = await sc.Manager.GetClient().ResolveReaderDeviceId(accessPointId);
        Assert.Equal(deviceId, reader.GetProperty("Guid").GetGuid());
        return new ReaderSettingsSnapshot(
            reader.GetProperty("CardAndPinMode").GetString()!,
            reader.GetProperty("CardAndPinSchedule").GetGuid(),
            TimeSpan.Parse(reader.GetProperty("CardAndPinTimeout").GetString()!, CultureInfo.InvariantCulture));
    }

    private sealed record ReaderSettingsSnapshot(string Mode, Guid ScheduleId, TimeSpan Timeout);

    private async Task<Guid?> GetUnbackedAccessPointIdAsync()
    {
        var json = await sc.Manager.GetClient().ListEntities("AccessPoint", pageSize: 50, page: 1);
        foreach (var id in IntegrationTestHelpers.ExtractGuids(json))
        {
            var entity = await sc.Manager.GetClient().GetEntity(id.ToString());
            using var document = JsonDocument.Parse(entity);
            var root = document.RootElement;
            if (!IsCardReader(root))
                continue;
            if (!root.TryGetProperty("Device", out var device) ||
                device.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(device.GetString(), out var deviceId) ||
                deviceId == Guid.Empty)
                return id;
        }

        return null;
    }

    private async Task<Guid?> GetFirstAssuranceProfileIdAsync()
    {
        // Assurance profiles are system configuration records, not entities.
        // EnableAssuranceProfile accepts ProfileId rather than an entity GUID.
        var json = Assert.IsType<string>(await AssuranceProfileListTool.Execute(sc.Manager));
        using var document = IntegrationTestHelpers.ParseJson(json);
        var profiles = IntegrationTestHelpers.GetResult(document.RootElement)
            .GetProperty("GetAssuranceProfiles");
        foreach (var profile in profiles.EnumerateArray())
        {
            var id = profile.GetProperty("ProfileId").GetGuid();
            if (id != Guid.Empty)
                return id;
        }

        return null;
    }

    private async Task<Guid?> GetEntityIdByNameAsync(string entityType, string name)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType, returnFields: ["Name"]);
        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("Body", out var body) ? body : default;

        if (rows.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("Name", out var rowName) || rowName.GetString() != name)
                continue;
            if (row.TryGetProperty("Guid", out var guid) && Guid.TryParse(guid.GetString(), out var id))
                return id;
        }

        return null;
    }

    private static bool IsCardReader(JsonElement entity)
        => entity.TryGetProperty("AccessPointType", out var type) &&
           string.Equals(type.GetString(), "CardReader", StringComparison.OrdinalIgnoreCase);
}
