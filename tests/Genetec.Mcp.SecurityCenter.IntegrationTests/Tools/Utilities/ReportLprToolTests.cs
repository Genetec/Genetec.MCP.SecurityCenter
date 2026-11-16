using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class ReportLprToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    private const string TimeStart = "2024-01-01T00:00:00Z";
    private const string TimeEnd = "2024-01-02T00:00:00Z";

    [Fact]
    public async Task LprHit_WithTypedFiltersAndTimeRange_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var plate = $"LPR{Guid.NewGuid():N}"[..10];
        var sourceId = await GetRequiredEntityIdAsync("LprUnit");

        var result = await ReportLprTool.Execute(sc.Manager,
            new ReportLprRequest(LprReportType.LprHit, TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(LprHit: new ReportLprHitFilters(
                    SourceIds: [sourceId],
                    HitTypes: [LprHitType.Hotlist, LprHitType.Overtime], Plates: [plate], PartialMatchPlates: false))));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task PatrollerPositions_WithTypedPatrollerFilter_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var patrollerId = await GetRequiredEntityIdAsync("Patroller");

        var result = await ReportLprTool.Execute(sc.Manager,
            new ReportLprRequest(LprReportType.PatrollerPositions,
                TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(PatrollerPositions: new ReportLprPatrollerPositionFilters(
                    [patrollerId]))));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task LpmDailyUsage_WithPatrollerAndRelativeRangeFilter_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var patrollerId = await GetRequiredEntityIdAsync("Patroller");

        var result = await ReportLprTool.Execute(sc.Manager,
            new ReportLprRequest(LprReportType.LpmDailyUsage,
                PatrollerId: patrollerId, TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(LpmDailyUsage: new ReportLprDailyUsageFilters(
                    RelativeTimeRange: false, RefTimeZoneId: "UTC"))));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task ParkingSession_WithTypedZonePlateStateAndTimeFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var parkingZoneId = await GetRequiredEntityIdAsync("ParkingZone");

        var result = await ReportLprTool.Execute(sc.Manager,
            new ReportLprRequest(LprReportType.ParkingSession,
                TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(ParkingSession: new ReportLprParkingSessionFilters(
                    [parkingZoneId], Plates: ["LPRTEST"], PartialMatchPlates: false,
                    SessionStates: [LprSessionState.Valid], MinimumDurationOfStay: TimeSpan.Zero))));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task ParkingInventory_WithTypedGranularityFilter_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var parkingZoneId = await GetRequiredEntityIdAsync("ParkingZone");

        var result = await ReportLprTool.Execute(sc.Manager,
            new ReportLprRequest(LprReportType.ParkingInventory,
                Filters: new ReportLprFilters(ParkingInventory: new ReportLprParkingInventoryFilters(
                    [parkingZoneId], Granularity: LprParkingOccupancyGranularity.Hour, MaximumRowsPerBatch: 100))));

        AssertValidReportResult(result);
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private async Task<Guid> GetRequiredEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType, 1, 1);
        var id = IntegrationTestHelpers.ExtractFirstGuid(json);
        Assert.True(id.HasValue, $"No {entityType} entities are configured for integration tests.");
        return id.Value;
    }

    private static void AssertValidReportResult(object result)
    {
        var json = Assert.IsType<string>(result);
        Assert.NotEmpty(json);
        using var _ = JsonDocument.Parse(json);
    }
}
