using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class ParkingOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task WrongZoneType_RejectsAndPreservesUserGroup()
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        foreach (var action in Enum.GetValues<ParkingOperationsAction>())
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingOperationsTool.Execute(sc.Manager,
                new ParkingOperationsRequest(wrong.Id, action, Occupancy: 10, SessionIds: [Guid.NewGuid()])));
            Assert.Equal("zone_id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task ResetInventory_ValidZone_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var zoneId = await GetFirstZoneIdAsync();
        Assert.NotNull(zoneId);

        var result = await ParkingOperationsTool.Execute(sc.Manager,
            new ParkingOperationsRequest(zoneId!.Value, ParkingOperationsAction.ResetInventory));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetOccupancy_ValidZone_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var zoneId = await GetFirstZoneIdAsync();
        Assert.NotNull(zoneId);

        var result = await ParkingOperationsTool.Execute(sc.Manager,
            new ParkingOperationsRequest(zoneId!.Value, ParkingOperationsAction.SetOccupancy, Occupancy: 10));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task TriggerEnforceViolation_ValidZone_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var zoneId = await GetFirstZoneIdAsync();
        Assert.NotNull(zoneId);

        // Parking sessions are dynamic records; a non-zero GUID validates the parameter
        // contract without requiring an active LPR session in the test environment.
        var fakeSessionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var result = await ParkingOperationsTool.Execute(sc.Manager,
            new ParkingOperationsRequest(zoneId!.Value, ParkingOperationsAction.TriggerEnforceViolation,
                SessionIds: [fakeSessionId]));

        Assert.NotNull(result);
    }

    private async Task<Guid?> GetFirstZoneIdAsync()
    {
        var zones = await sc.Manager.GetClient().ListEntities("ParkingZone");
        return ScIntegrationFixture.ExtractFirstGuid(zones);
    }
}
