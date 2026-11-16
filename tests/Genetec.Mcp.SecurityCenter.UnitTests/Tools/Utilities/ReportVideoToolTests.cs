using Genetec.Mcp.SecurityCenter.Tools;
using System.Net;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class ReportVideoToolTests
{
    private const string TimeStart = "2024-01-01T00:00:00Z";
    private const string TimeEnd = "2024-01-02T00:00:00Z";
    private static readonly Guid EntityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }

    [Fact]
    public void SupportedReportTypes_ContainEveryReportVideoSubtool()
    {
        Assert.Equal(
            [
                VideoReportType.MotionEvent,
                VideoReportType.BlockingVideo,
                VideoReportType.RecordingEvent,
                VideoReportType.VideoMotion,
                VideoReportType.ArchiverStatistics,
                VideoReportType.CameraConfiguration,
                VideoReportType.ArchiveTransfer,
                VideoReportType.TransferHistory,
                VideoReportType.FileDeletion
            ],
            Enum.GetValues<VideoReportType>());
    }

    [Theory]
    [InlineData(VideoReportType.MotionEvent)]
    [InlineData(VideoReportType.BlockingVideo)]
    [InlineData(VideoReportType.RecordingEvent)]
    [InlineData(VideoReportType.VideoMotion)]
    [InlineData(VideoReportType.ArchiverStatistics)]
    [InlineData(VideoReportType.CameraConfiguration)]
    [InlineData(VideoReportType.FileDeletion)]
    public async Task IdReport_MissingId_ThrowsArgumentException(VideoReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportVideoTool.Execute(TestHelpers.FakeManager(),
                new ReportVideoRequest(reportType,
                    TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z")));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VideoReportType.MotionEvent)]
    [InlineData(VideoReportType.BlockingVideo)]
    [InlineData(VideoReportType.RecordingEvent)]
    [InlineData(VideoReportType.VideoMotion)]
    public async Task TimeReport_MissingTimeStart_ThrowsArgumentException(VideoReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportVideoTool.Execute(TestHelpers.FakeManager(),
                new ReportVideoRequest(reportType,
                    Id: Guid.NewGuid(), TimeEnd: "2024-01-02T00:00:00Z")));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VideoReportType.MotionEvent)]
    [InlineData(VideoReportType.BlockingVideo)]
    [InlineData(VideoReportType.RecordingEvent)]
    [InlineData(VideoReportType.VideoMotion)]
    public async Task TimeRangeReport_MissingTimeEnd_ThrowsArgumentException(VideoReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportVideoTool.Execute(TestHelpers.FakeManager(),
                new ReportVideoRequest(reportType,
                    Id: Guid.NewGuid(), TimeStart: "2024-01-01T00:00:00Z")));

        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransferHistory_WithOnlyOneTimeBoundary_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportVideoTool.Execute(TestHelpers.FakeManager(),
                new ReportVideoRequest(VideoReportType.TransferHistory,
                    Id: EntityId,
                    TimeStart: "2024-01-01T00:00:00Z")));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownReportType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportVideoTool.Execute(TestHelpers.FakeManager(),
                new ReportVideoRequest((VideoReportType)999)));

        Assert.Contains("Unknown report type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VideoReportType.MotionEvent, "MotionEvent", "Cameras@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(VideoReportType.BlockingVideo, "BlockingVideoEvent", "Cameras@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(VideoReportType.RecordingEvent, "RecordingEvent", "Cameras@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(VideoReportType.VideoMotion, "VideoMotion", "Cameras@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(VideoReportType.ArchiverStatistics, "ArchiverStatisticsReport", "Archivers@{id}")]
    [InlineData(VideoReportType.CameraConfiguration, "CameraConfiguration", "Cameras@{id}")]
    [InlineData(VideoReportType.ArchiveTransfer, "ArchiveTransferReport", "TransferGroupIds@{id}")]
    [InlineData(VideoReportType.TransferHistory, "TransferHistoryReport", "TransferGroupsToQuery@{id},ResultsTimeRange=DateTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(VideoReportType.FileDeletion, "FileDeletionEvent", "Cameras@{id},TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    public async Task ReportType_UsesExpectedWebSdkRouteAndQuery(
        VideoReportType reportType, string sdkReportType, string expectedQueryTemplate)
    {
        var handler = new CapturingHandler();
        var result = await ReportVideoTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportVideoRequest(
                reportType,
                Id: EntityId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd));

        Assert.Equal("[]", result);
        Assert.NotNull(handler.LastRequest);
        var expectedQuery = expectedQueryTemplate.Replace("{id}", EntityId.ToString(), StringComparison.Ordinal);
        var requestUri = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/{sdkReportType}?q={expectedQuery}",
            requestUri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VideoReportType.ArchiverStatistics, "ArchiverStatisticsReport", "Archivers@{id},MaximumResultCount=25")]
    [InlineData(VideoReportType.CameraConfiguration, "CameraConfiguration", "Cameras@{id},MaximumResultCount=25")]
    [InlineData(VideoReportType.ArchiveTransfer, "ArchiveTransferReport", "TransferGroupIds@{id},MaximumResultCount=25")]
    [InlineData(VideoReportType.TransferHistory, "TransferHistoryReport", "TransferGroupsToQuery@{id},MaximumResultCount=25")]
    public async Task MaximumResultCount_IsSentAsNamedQueryParameter(
        VideoReportType reportType, string sdkReportType, string expectedQueryTemplate)
    {
        var handler = new CapturingHandler();
        await ReportVideoTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportVideoRequest(reportType, Id: EntityId, MaximumResultCount: 25));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/{sdkReportType}?q={expectedQueryTemplate.Replace("{id}", EntityId.ToString(), StringComparison.Ordinal)}",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task VideoReport_UsesTypedWebSdkFilters()
    {
        var handler = new CapturingHandler();

        await ReportVideoTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportVideoRequest(
                VideoReportType.VideoMotion,
                Id: EntityId,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 25,
                Filters: new ReportVideoFilters(
                    SortOrder: ReportSortOrder.Descending,
                    MotionThreshold: 40,
                    ConsecutivesFrames: 3,
                    TimeBetweenFrames: 250)));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/VideoMotion?q=Cameras@{EntityId},ConsecutivesFrames=3,MotionThreshold=40,TimeBetweenFrames=250," +
            $"TimeRange.SetTimeRange({TimeStart},{TimeEnd}),SortOrder=Descending,MaximumResultCount=25",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferHistory_UsesTypedTransferFilters()
    {
        var handler = new CapturingHandler();

        await ReportVideoTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportVideoRequest(
                VideoReportType.TransferHistory,
                Id: EntityId,
                MaximumResultCount: 25,
                Filters: new ReportVideoFilters(
                    TransferResults: [TransferResultFilter.Success, TransferResultFilter.Partial],
                    TransferTypes: [TransferTypeFilter.ArchiverToDisk],
                    TriggerReasons: [TriggerReasonFilter.Manual],
                    ResultsCount: 2)));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/TransferHistoryReport?q=TransferGroupsToQuery@{EntityId}," +
            "TransferResultsFilter@Success@Partial,TransferTypeFilter@ArchiverToDisk," +
            "TriggerReasonFilter@Manual,ResultsCount=2,MaximumResultCount=25",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileDeletion_UsesTypedFileFilters()
    {
        var handler = new CapturingHandler();

        await ReportVideoTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportVideoRequest(
                VideoReportType.FileDeletion,
                Id: EntityId,
                MaximumResultCount: 25,
                Filters: new ReportVideoFilters(
                    FileFilter: "*.g64",
                    DateTimeFilter: TimeStart)));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/FileDeletionEvent?q=Cameras@{EntityId},FileFilter=*.g64,DateTimeFilter={TimeStart},MaximumResultCount=25",
            query,
            StringComparison.Ordinal);
    }
}
