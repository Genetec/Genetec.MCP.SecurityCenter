using Genetec.Mcp.SecurityCenter.Helpers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class ReportVideoTool
{
    [McpServerTool(
        Name = "report_video",
        Title = "Video Surveillance Reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Run video surveillance reports: motion_event, blocking_video, recording_event, video_motion, " +
                 "archiver_statistics, camera_configuration, archive_transfer, transfer_history, file_deletion. Use small result limits; do not fetch all entities automatically. Ask the user before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Video report request")] ReportVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        static string? BuildDateTimeRange(string? start, string? end) =>
            start is not null && end is not null
                ? $"ResultsTimeRange=DateTimeRange({Esc(start)},{Esc(end)})"
                : null;

        static string? BuildDuration(TimeSpan? duration) =>
            duration is null
                ? null
                : $"TotalDuration={Esc(duration.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture))}";

        static string? BuildEvents(List<ScEventType>? events) =>
            QueryCollection("Events", events);

        static string? BuildSortOrder(ReportSortOrder? sortOrder) =>
            sortOrder is null ? null : $"SortOrder={sortOrder.Value}";

        static string? BuildMaximumResultCount(int? maximumResultCount) =>
            QueryValue("MaximumResultCount", maximumResultCount);

        static string? BuildTransferResults(List<TransferResultFilter>? values) =>
            QueryCollection("TransferResultsFilter", values);

        static string? BuildTransferTypes(List<TransferTypeFilter>? values) =>
            QueryCollection("TransferTypeFilter", values);

        static string? BuildTriggerReasons(List<TriggerReasonFilter>? values) =>
            QueryCollection("TriggerReasonFilter", values);

        static string? BuildResultsCount(int? resultsCount) =>
            QueryValue("ResultsCount", resultsCount);

        static string? BuildOptionalDateTimeFilter(ReportVideoRequest reportRequest, string? dateTimeFilter)
        {
            if (dateTimeFilter is not null)
                return QueryValue("DateTimeFilter", dateTimeFilter);

            // Preserve the original report_video contract where time_start was
            // the single timestamp filter for file_deletion.
            return reportRequest.TimeStart is not null && reportRequest.TimeEnd is null
                ? QueryValue("DateTimeFilter", reportRequest.TimeStart)
                : null;
        }

        var client = sessionManager.GetClient(cancellationToken);

        switch (request.ReportType)
        {
            case VideoReportType.MotionEvent:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for motion_event.");
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for motion_event.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for motion_event.");
                return await client.RunReport("MotionEvent",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        BuildEvents(request.Filters?.Events),
                        QueryValue("MotionThreshold", request.Filters?.MotionThreshold),
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.BlockingVideo:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for blocking_video.");
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for blocking_video.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for blocking_video.");
                return await client.RunReport("BlockingVideoEvent",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        BuildEvents(request.Filters?.Events),
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.RecordingEvent:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for recording_event.");
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for recording_event.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for recording_event.");
                return await client.RunReport("RecordingEvent",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        BuildEvents(request.Filters?.Events),
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.VideoMotion:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for video_motion.");
                if (request.TimeStart is null) throw new ArgumentException("Parameter 'time_start' is required for video_motion.");
                if (request.TimeEnd is null) throw new ArgumentException("Parameter 'time_end' is required for video_motion.");
                return await client.RunReport("VideoMotion",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        QueryValue("ConsecutivesFrames", request.Filters?.ConsecutivesFrames),
                        QueryValue("MotionThreshold", request.Filters?.MotionThreshold),
                        QueryValue("TimeBetweenFrames", request.Filters?.TimeBetweenFrames),
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.ArchiverStatistics:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for archiver_statistics.");
                return await client.RunReport("ArchiverStatisticsReport",
                    BuildQuery(
                        $"Archivers@{request.Id}",
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.CameraConfiguration:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for camera_configuration.");
                return await client.RunReport("CameraConfiguration",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.ArchiveTransfer:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for archive_transfer.");
                return await client.RunReport("ArchiveTransferReport",
                    BuildQuery(
                        $"TransferGroupIds@{request.Id}",
                        QueryValue("OnlyActiveTransfers", request.Filters?.OnlyActiveTransfers),
                        BuildDuration(request.Filters?.TotalDuration),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.TransferHistory:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for transfer_history.");
                if ((request.TimeStart is null) != (request.TimeEnd is null))
                    throw new ArgumentException("Parameters 'time_start' and 'time_end' must be provided together for transfer_history.");

                return await client.RunReport("TransferHistoryReport",
                    BuildQuery(
                        $"TransferGroupsToQuery@{request.Id}",
                        BuildTransferResults(request.Filters?.TransferResults),
                        BuildTransferTypes(request.Filters?.TransferTypes),
                        BuildTriggerReasons(request.Filters?.TriggerReasons),
                        BuildDateTimeRange(request.TimeStart, request.TimeEnd),
                        BuildResultsCount(request.Filters?.ResultsCount),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            case VideoReportType.FileDeletion:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for file_deletion.");
                var fileDeletionTimeRange = request.TimeStart is not null && request.TimeEnd is not null
                    ? QueryTimeRange(request.TimeStart, request.TimeEnd)
                    : null;
                return await client.RunReport("FileDeletionEvent",
                    BuildQuery(
                        $"Cameras@{request.Id}",
                        QueryValue("FileFilter", request.Filters?.FileFilter),
                        BuildOptionalDateTimeFilter(request, request.Filters?.DateTimeFilter),
                        fileDeletionTimeRange,
                        BuildSortOrder(request.Filters?.SortOrder),
                        BuildMaximumResultCount(request.MaximumResultCount)));

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }
    }
}

public enum VideoReportType
{
    MotionEvent,
    BlockingVideo,
    RecordingEvent,
    VideoMotion,
    ArchiverStatistics,
    CameraConfiguration,
    ArchiveTransfer,
    TransferHistory,
    FileDeletion
}

public enum TransferResultFilter
{
    Unknown,
    Success,
    Fail,
    Partial,
    Canceled,
    Active
}

public enum TransferTypeFilter
{
    EdgeToArchiver,
    ArchiverToArchiver,
    ArchiverToDisk,
    DiskToArchiver,
    EdgeFileToArchiver,
    ArchiveConsolidation
}

public enum TriggerReasonFilter
{
    Manual,
    Scheduled
}

public sealed record ReportVideoRequest(
    [property: Description("Report type to run"), Required] VideoReportType ReportType,
    [property: Description("Required entity GUID: camera for motion_event, blocking_video, recording_event, video_motion, camera_configuration, and file_deletion; archiver for archiver_statistics; transfer group for archive_transfer and transfer_history"), Required] Guid? Id = null,
    [property: Description("Report start time ISO 8601"), Iso8601] string? TimeStart = null,
    [property: Description("Report end time ISO 8601"), Iso8601] string? TimeEnd = null,
    [property: Description("Maximum number of results when supported by the report (default 50); use small limits and ask the user before large queries"), Range(1, 100000)] int MaximumResultCount = 50,
    [property: Description("Typed report-specific filters; fields are documented on ReportVideoFilters")] ReportVideoFilters? Filters = null);

public sealed record ReportVideoFilters(
    [property: Description("Event type IDs to include for motion_event, blocking_video, and recording_event"), MaxLength(ResourceLimits.MaxBatchItems)]
    List<ScEventType>? Events = null,
    [property: Description("Result order where supported by the report")] ReportSortOrder? SortOrder = null,
    [property: Description("Motion threshold for motion_event and video_motion"), Range(0, 100)] int? MotionThreshold = null,
    [property: Description("Consecutive frames required by video_motion"), Range(1, int.MaxValue)] int? ConsecutivesFrames = null,
    [property: Description("Milliseconds between frames for video_motion"), Range(1, int.MaxValue)] int? TimeBetweenFrames = null,
    [property: Description("File name or pattern for file_deletion")] string? FileFilter = null,
    [property: Description("Specific file timestamp filter for file_deletion, ISO 8601"), Iso8601] string? DateTimeFilter = null,
    [property: Description("Whether archive_transfer should return only active transfer groups")] bool? OnlyActiveTransfers = null,
    [property: Description("Total archive_transfer query duration as hh:mm:ss")] TimeSpan? TotalDuration = null,
    [property: Description("Transfer result values to include for transfer_history"), MaxLength(ResourceLimits.MaxBatchItems)]
    List<TransferResultFilter>? TransferResults = null,
    [property: Description("Transfer type values to include for transfer_history"), MaxLength(ResourceLimits.MaxBatchItems)]
    List<TransferTypeFilter>? TransferTypes = null,
    [property: Description("Trigger reason values to include for transfer_history"), MaxLength(ResourceLimits.MaxBatchItems)]
    List<TriggerReasonFilter>? TriggerReasons = null,
    [property: Description("Number of results per transfer group for transfer_history (default 50); ask the user before large queries"), Range(1, 100000)] int ResultsCount = 50);
