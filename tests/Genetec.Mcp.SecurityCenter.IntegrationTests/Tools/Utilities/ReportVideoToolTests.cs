using Genetec.Mcp.SecurityCenter.Tools;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class ReportVideoToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    private const string TimeStart = "2024-01-01T00:00:00Z";
    private const string TimeEnd = "2024-01-02T00:00:00Z";

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(VideoReportType.MotionEvent)]
    [InlineData(VideoReportType.BlockingVideo)]
    [InlineData(VideoReportType.RecordingEvent)]
    [InlineData(VideoReportType.VideoMotion)]
    public async Task CameraTimeReport_WithTimeRange_ReturnsValidJson(VideoReportType reportType)
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(reportType, Id: cameraId,
                TimeStart: TimeStart, TimeEnd: TimeEnd));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task BlockingVideo_WithCameraAndTimeRange_ReturnsBlockingVideoReportResult()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.BlockingVideo,
                Id: cameraId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    Events: [ScEventType.CameraMotionOn],
                    SortOrder: ReportSortOrder.Descending)));

        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        Assert.Equal(JsonValueKind.Array, IntegrationTestHelpers.GetResult(document.RootElement).ValueKind);
    }

    [Fact]
    public async Task MotionEvent_WithTypedFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.MotionEvent,
                Id: cameraId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    Events: [ScEventType.CameraMotionOn],
                    MotionThreshold: 50,
                    SortOrder: ReportSortOrder.Ascending)));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task RecordingEvent_WithTypedFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.RecordingEvent,
                Id: cameraId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    Events: [ScEventType.CameraMotionOn],
                    SortOrder: ReportSortOrder.Descending)));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task VideoMotion_WithTypedFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.VideoMotion,
                Id: cameraId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    ConsecutivesFrames: 1,
                    MotionThreshold: 50,
                    TimeBetweenFrames: 100,
                    SortOrder: ReportSortOrder.Descending)));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task ArchiverStatistics_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var archiverId = await GetRequiredArchiverIdAsync();

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.ArchiverStatistics, Id: archiverId,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(SortOrder: ReportSortOrder.Descending)));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task CameraConfiguration_WithCamera_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.CameraConfiguration, Id: cameraId,
                MaximumResultCount: 10));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task ArchiveTransfer_WithTransferGroupAndTypedFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var transferGroupId = await GetRequiredTransferGroupIdAsync();

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.ArchiveTransfer, Id: transferGroupId,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    OnlyActiveTransfers: false,
                    TotalDuration: TimeSpan.FromMinutes(1))));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task TransferHistory_WithTransferGroupAndTypedFilters_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var transferGroupId = await GetRequiredTransferGroupIdAsync();

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.TransferHistory,
                Id: transferGroupId,
                TimeStart: TimeStart, TimeEnd: TimeEnd, MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    TransferResults: [TransferResultFilter.Success],
                    TransferTypes: [TransferTypeFilter.ArchiverToDisk],
                    TriggerReasons: [TriggerReasonFilter.Manual],
                    ResultsCount: 1)));

        AssertValidReportResult(result);
    }

    [Fact]
    public async Task FileDeletion_WithTimeStart_ReturnsValidJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetRequiredEntityIdAsync("Camera");

        var result = await ReportVideoTool.Execute(sc.Manager,
            new ReportVideoRequest(VideoReportType.FileDeletion, Id: cameraId,
                MaximumResultCount: 10,
                Filters: new ReportVideoFilters(
                    FileFilter: "*.g64",
                    DateTimeFilter: TimeStart,
                    SortOrder: ReportSortOrder.Descending)));

        AssertValidReportResult(result);
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private async Task<Guid> GetRequiredEntityIdAsync(string entityType)
    {
        if (entityType == "Camera")
            return await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        var json = await sc.Manager.GetClient().ListEntities(entityType, 1, 1);
        var id = IntegrationTestHelpers.ExtractFirstGuid(json);
        Assert.True(id.HasValue, $"No {entityType} entities are configured for integration tests.");
        return id.Value;
    }

    private async Task<Guid> GetRequiredArchiverIdAsync()
    {
        var rolesJson = await sc.Manager.GetClient().ListEntities("Role");
        using var rolesDocument = IntegrationTestHelpers.ParseJson(rolesJson);
        var rows = IntegrationTestHelpers.GetResult(rolesDocument.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(rolesDocument.RootElement)
            : IntegrationTestHelpers.GetResult(rolesDocument.RootElement).GetProperty("Body");

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("Guid", out var guidProperty) ||
                !Guid.TryParse(guidProperty.GetString(), out var roleId))
                continue;

            var entityJson = await sc.Manager.GetClient().GetEntityTrimmed(roleId.ToString());
            using var entityDocument = IntegrationTestHelpers.ParseJson(entityJson);
            var entity = IntegrationTestHelpers.GetResult(entityDocument.RootElement);
            if (entity.TryGetProperty("Type", out var typeProperty) &&
                string.Equals(typeProperty.GetString(), "Archiver", StringComparison.OrdinalIgnoreCase))
                return roleId;
        }

        Assert.Fail("No Archiver role is configured for integration tests.");
        return default;
    }

    private async Task<Guid> GetRequiredTransferGroupIdAsync()
    {
        var json = await sc.Manager.GetClient().ListEntities("TransferGroup", 1, 1);
        var id = IntegrationTestHelpers.ExtractFirstGuid(json);
        Assert.True(id.HasValue, "No TransferGroup entities are configured for integration tests.");
        return id.Value;
    }

    private static void AssertValidReportResult(object result)
    {
        var json = Assert.IsType<string>(result);
        Assert.NotEmpty(json);
        using var _ = JsonDocument.Parse(json);
    }
}
