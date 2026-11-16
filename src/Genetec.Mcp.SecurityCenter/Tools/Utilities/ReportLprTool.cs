using Genetec.Mcp.SecurityCenter.Helpers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class ReportLprTool
{
    [McpServerTool(
        Name = "report_lpr",
        Title = "LPR Reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Run LPR and parking reports: lpr_hit, patroller_positions, lpm_daily_usage, parking_session, parking_inventory. lpr_hit requires filters.lpr_hit.source_ids to route the report. Use small result limits; do not fetch all entities automatically. Ask the user before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("LPR report request")] ReportLprRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.ReportType)
        {
            case LprReportType.LprHit:
                EnsureNoMismatchedFilters(request, LprReportType.LprHit);
                EnsureTimeRange(request, required: true);
                EnsureCollection(request.Filters?.LprHit?.SourceIds, "filters.lpr_hit.source_ids", LprReportType.LprHit);
                EnsureOptionalTimePair(request.Filters?.LprHit?.ImportedTimeStart, request.Filters?.LprHit?.ImportedTimeEnd,
                    "imported_time_start", "imported_time_end");
                return await client.RunReport("LprHit", BuildQuery(
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", request.MaximumResultCount),
                    QueryValue("SortOrder", request.SortOrder),
                    ActivityReportQuery.CustomFields(request.Filters?.CustomFields),
                    QueryCollection("ExcludedSourceIds", request.Filters?.LprHit?.ExcludedSourceIds),
                    QueryCollection("HitTypes", request.Filters?.LprHit?.HitTypes),
                    QueryTimeRange(request.Filters?.LprHit?.ImportedTimeStart, request.Filters?.LprHit?.ImportedTimeEnd, "ImportedTimeRange"),
                    QueryCollection("LprRuleIds", request.Filters?.LprHit?.LprRuleIds),
                    BuildCustomFieldFilters(request.Filters?.LprHit?.CustomFieldFilters),
                    QueryValue("PartialMatchPlates", request.Filters?.LprHit?.PartialMatchPlates),
                    QueryCollection("Plates", request.Filters?.LprHit?.Plates),
                    QueryCollection("ReasonTypes", request.Filters?.LprHit?.ReasonTypes),
                    QueryCollection("RejectReasons", request.Filters?.LprHit?.RejectReasons),
                    QueryValue("RetrieveImagesOptions", request.Filters?.LprHit?.RetrieveImagesOptions),
                    QueryCollection("SourceIds", request.Filters?.LprHit?.SourceIds),
                    QueryCollection("UserActions", request.Filters?.LprHit?.UserActions),
                    QueryCollection("UserGroupIds", request.Filters?.LprHit?.UserGroupIds),
                    QueryCollection("UserIds", request.Filters?.LprHit?.UserIds)));

            case LprReportType.PatrollerPositions:
                EnsureNoMismatchedFilters(request, LprReportType.PatrollerPositions);
                EnsureTimeRange(request, required: true);
                EnsureCollection(request.Filters?.PatrollerPositions?.Patrollers, "patrollers", LprReportType.PatrollerPositions);
                return await client.RunReport("PatrollerPositions", BuildQuery(
                    QueryCollection("Patrollers", request.Filters?.PatrollerPositions?.Patrollers),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", request.MaximumResultCount),
                    QueryValue("SortOrder", request.SortOrder),
                    ActivityReportQuery.CustomFields(request.Filters?.CustomFields)));

            case LprReportType.LpmDailyUsage:
                EnsureNoMismatchedFilters(request, LprReportType.LpmDailyUsage);
                EnsureTimeRange(request, required: true);
                if (request.PatrollerId is null)
                    throw new ArgumentException("Parameter 'patroller_id' is required for lpm_daily_usage.");
                return await client.RunReport("LpmDailyUsage", BuildQuery(
                    QueryValue("PatrollerId", request.PatrollerId),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("RelativeTimeRange", request.Filters?.LpmDailyUsage?.RelativeTimeRange),
                    QueryValue("RefTimeZone", SerializeTimeZone(request.Filters?.LpmDailyUsage?.RefTimeZoneId)),
                    QueryValue("MaximumResultCount", request.MaximumResultCount),
                    QueryValue("SortOrder", request.SortOrder),
                    ActivityReportQuery.CustomFields(request.Filters?.CustomFields)));

            case LprReportType.ParkingSession:
                EnsureNoMismatchedFilters(request, LprReportType.ParkingSession);
                EnsureTimeRange(request, required: true);
                EnsureCollection(request.Filters?.ParkingSession?.ParkingZones, "parking_zones", LprReportType.ParkingSession);
                return await client.RunReport("ParkingSession", BuildQuery(
                    QueryCollection("ParkingZones", request.Filters?.ParkingSession?.ParkingZones),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MinimumDurationOfStay", FormatDuration(request.Filters?.ParkingSession?.MinimumDurationOfStay)),
                    QueryValue("MaximumDurationOfStay", FormatDuration(request.Filters?.ParkingSession?.MaximumDurationOfStay)),
                    QueryCollection("PermitIds", request.Filters?.ParkingSession?.PermitIds),
                    QueryValue("PartialMatchPlates", request.Filters?.ParkingSession?.PartialMatchPlates),
                    QueryCollection("Plates", request.Filters?.ParkingSession?.Plates),
                    QueryValue("RetrieveImagesOptions", request.Filters?.ParkingSession?.RetrieveImagesOptions),
                    QueryCollection("SessionStates", request.Filters?.ParkingSession?.SessionStates),
                    QueryValue("MaximumResultCount", request.MaximumResultCount),
                    QueryValue("SortOrder", request.SortOrder),
                    ActivityReportQuery.CustomFields(request.Filters?.CustomFields)));

            case LprReportType.ParkingInventory:
                EnsureNoMismatchedFilters(request, LprReportType.ParkingInventory);
                EnsureTimeRange(request, required: false);
                EnsureCollection(request.Filters?.ParkingInventory?.ParkingZones, "parking_zones", LprReportType.ParkingInventory);
                return await client.RunReport("ParkingZoneOccupancy", BuildQuery(
                    QueryCollection("ParkingZones", request.Filters?.ParkingInventory?.ParkingZones),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("Granularity", request.Filters?.ParkingInventory?.Granularity),
                    QueryValue("MaximumRowsPerBatch", request.Filters?.ParkingInventory?.MaximumRowsPerBatch),
                    QueryValue("RetrieveImagesOptions", request.Filters?.ParkingInventory?.RetrieveImagesOptions),
                    QueryValue("MaximumResultCount", request.MaximumResultCount),
                    QueryValue("SortOrder", request.SortOrder),
                    ActivityReportQuery.CustomFields(request.Filters?.CustomFields)));

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }
    }

    private static string? BuildCustomFieldFilters(List<ReportLprCustomFieldFilter>? filters)
    {
        if (filters is not { Count: > 0 })
            return null;

        var entries = filters.Select(filter => $"{{{Esc(filter.FieldId.ToString("D", CultureInfo.InvariantCulture))},{Esc(filter.Value)}}}");
        return "CustomFieldFilters={" + string.Join(",", entries) + "}";
    }

    private static string? FormatDuration(TimeSpan? duration) =>
        duration?.ToString("c", CultureInfo.InvariantCulture);

    private static string? SerializeTimeZone(string? timeZoneId)
    {
        if (timeZoneId is null)
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).ToSerializedString();
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException("Parameter 'ref_time_zone_id' must be a time zone known to the system.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new ArgumentException("Parameter 'ref_time_zone_id' must be a valid time zone.", ex);
        }
    }

    private static void EnsureOptionalTimePair(string? start, string? end, string startName, string endName)
    {
        if ((start is null) != (end is null))
            throw new ArgumentException($"Parameters '{startName}' and '{endName}' must be provided together.");
    }

    private static void EnsureNoMismatchedFilters(ReportLprRequest request, LprReportType expected)
    {
        var mismatchedFilterName = expected switch
        {
            LprReportType.LprHit when request.Filters?.PatrollerPositions is not null => "patroller_positions",
            LprReportType.LprHit when request.Filters?.LpmDailyUsage is not null => "lpm_daily_usage",
            LprReportType.LprHit when request.Filters?.ParkingSession is not null => "parking_session",
            LprReportType.LprHit when request.Filters?.ParkingInventory is not null => "parking_inventory",
            LprReportType.PatrollerPositions when request.Filters?.LprHit is not null => "lpr_hit",
            LprReportType.PatrollerPositions when request.Filters?.LpmDailyUsage is not null => "lpm_daily_usage",
            LprReportType.PatrollerPositions when request.Filters?.ParkingSession is not null => "parking_session",
            LprReportType.PatrollerPositions when request.Filters?.ParkingInventory is not null => "parking_inventory",
            LprReportType.LpmDailyUsage when request.Filters?.LprHit is not null => "lpr_hit",
            LprReportType.LpmDailyUsage when request.Filters?.PatrollerPositions is not null => "patroller_positions",
            LprReportType.LpmDailyUsage when request.Filters?.ParkingSession is not null => "parking_session",
            LprReportType.LpmDailyUsage when request.Filters?.ParkingInventory is not null => "parking_inventory",
            LprReportType.ParkingSession when request.Filters?.LprHit is not null => "lpr_hit",
            LprReportType.ParkingSession when request.Filters?.PatrollerPositions is not null => "patroller_positions",
            LprReportType.ParkingSession when request.Filters?.LpmDailyUsage is not null => "lpm_daily_usage",
            LprReportType.ParkingSession when request.Filters?.ParkingInventory is not null => "parking_inventory",
            LprReportType.ParkingInventory when request.Filters?.LprHit is not null => "lpr_hit",
            LprReportType.ParkingInventory when request.Filters?.PatrollerPositions is not null => "patroller_positions",
            LprReportType.ParkingInventory when request.Filters?.LpmDailyUsage is not null => "lpm_daily_usage",
            LprReportType.ParkingInventory when request.Filters?.ParkingSession is not null => "parking_session",
            _ => null
        };

        if (mismatchedFilterName is not null)
            throw new ArgumentException($"Filter object '{mismatchedFilterName}' is not supported for {ToSchemaName(expected)}.");

        if (request.PatrollerId is not null && expected != LprReportType.LpmDailyUsage)
            throw new ArgumentException("Parameter 'patroller_id' is only supported for lpm_daily_usage.");
    }

    private static void EnsureTimeRange(ReportLprRequest request, bool required)
    {
        if ((request.TimeStart is null) != (request.TimeEnd is null))
            throw new ArgumentException("Parameters 'time_start' and 'time_end' must be provided together.");

        if (!required)
            return;

        if (request.TimeStart is null)
            throw new ArgumentException($"Parameter 'time_start' is required for {ToSchemaName(request.ReportType)}.");
        if (request.TimeEnd is null)
            throw new ArgumentException($"Parameter 'time_end' is required for {ToSchemaName(request.ReportType)}.");
    }

    private static void EnsureCollection<T>(List<T>? values, string parameterName, LprReportType reportType)
    {
        if (values is not { Count: > 0 })
            throw new ArgumentException($"Parameter '{parameterName}' must contain at least one value for {ToSchemaName(reportType)}.");
    }

    private static string ToSchemaName(LprReportType reportType) => reportType switch
    {
        LprReportType.LprHit => "lpr_hit",
        LprReportType.PatrollerPositions => "patroller_positions",
        LprReportType.LpmDailyUsage => "lpm_daily_usage",
        LprReportType.ParkingSession => "parking_session",
        LprReportType.ParkingInventory => "parking_inventory",
        _ => reportType.ToString()
    };
}

public enum LprReportType { LprHit, PatrollerPositions, LpmDailyUsage, ParkingSession, ParkingInventory }

public enum LprHitType { Hotlist, Overtime, Permit, SharedPermit }
public enum LprHitReason { None }
public enum LprHitUserAction { Accepted, Rejected, NotEnforced, Enforced }
public enum LprRetrieveImagesOptions { None, FullSize, Thumbnail }
public enum LprSessionState { Completed, Enforced, Violation, Valid }
public enum LprParkingOccupancyGranularity { Minute, Hour, Day }

public sealed record ReportLprRequest(
    [property: Description("Report type to run"), Required] LprReportType ReportType,
    [property: Description("Report start time ISO 8601"), Iso8601] string? TimeStart = null,
    [property: Description("Report end time ISO 8601"), Iso8601] string? TimeEnd = null,
    [property: Description("Maximum report rows (default 50); use small limits and ask the user before large queries"), Range(1, 100000)] int MaximumResultCount = 50,
    [property: Description("Result order where supported by the report")] ReportSortOrder? SortOrder = null,
    [property: Description("Patroller GUID for lpm_daily_usage")] Guid? PatrollerId = null,
    [property: Description("Typed report-specific filters; provide only the filter group matching report_type")] ReportLprFilters? Filters = null);

public sealed record ReportLprFilters(
    [property: Description("Common custom-field filters supported by LPR reports"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportCustomFieldFilter>? CustomFields = null,
    [property: Description("Filters supported by lpr_hit only")] ReportLprHitFilters? LprHit = null,
    [property: Description("Filters supported by patroller_positions only")] ReportLprPatrollerPositionFilters? PatrollerPositions = null,
    [property: Description("Filters supported by lpm_daily_usage only")] ReportLprDailyUsageFilters? LpmDailyUsage = null,
    [property: Description("Filters supported by parking_session only")] ReportLprParkingSessionFilters? ParkingSession = null,
    [property: Description("Filters supported by parking_inventory only")] ReportLprParkingInventoryFilters? ParkingInventory = null);

public sealed record ReportLprCustomFieldFilter(
    [property: Description("Custom field GUID"), Required] Guid FieldId,
    [property: Description("Text value to match in the LPR hit custom field"), Required] string Value);

public sealed record ReportLprHitFilters(
    [property: Description("ALPR unit, patroller, or ALPR device GUIDs to query; required to route the report"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> SourceIds,
    [property: Description("Excluded LPR units, patrollers, or devices"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ExcludedSourceIds = null,
    [property: Description("Custom-field GUID and text value filters for lpr_hit"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportLprCustomFieldFilter>? CustomFieldFilters = null,
    [property: Description("Hit types to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<LprHitType>? HitTypes = null,
    [property: Description("Imported hit start time ISO 8601"), Iso8601] string? ImportedTimeStart = null,
    [property: Description("Imported hit end time ISO 8601"), Iso8601] string? ImportedTimeEnd = null,
    [property: Description("LPR rule or permit GUIDs"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? LprRuleIds = null,
    [property: Description("Return reads containing partial plate matches")] bool? PartialMatchPlates = null,
    [property: Description("License plates to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Plates = null,
    [property: Description("Hit reasons to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<LprHitReason>? ReasonTypes = null,
    [property: Description("Hit rejection reasons to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? RejectReasons = null,
    [property: Description("Image retrieval mode")] LprRetrieveImagesOptions? RetrieveImagesOptions = null,
    [property: Description("User actions to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<LprHitUserAction>? UserActions = null,
    [property: Description("User-group GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? UserGroupIds = null,
    [property: Description("User GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? UserIds = null);

public sealed record ReportLprPatrollerPositionFilters(
    [property: Description("Patroller GUIDs"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Patrollers);

public sealed record ReportLprDailyUsageFilters(
    [property: Description("Use a relative time range")] bool? RelativeTimeRange = null,
    [property: Description("System time zone ID used to calculate the daily statistics")] string? RefTimeZoneId = null);

public sealed record ReportLprParkingSessionFilters(
    [property: Description("Parking-zone GUIDs"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> ParkingZones,
    [property: Description("Minimum parking duration")] TimeSpan? MinimumDurationOfStay = null,
    [property: Description("Maximum parking duration")] TimeSpan? MaximumDurationOfStay = null,
    [property: Description("Permit GUIDs"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? PermitIds = null,
    [property: Description("Match partial plate numbers")] bool? PartialMatchPlates = null,
    [property: Description("License plates to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<string>? Plates = null,
    [property: Description("Image retrieval mode")] LprRetrieveImagesOptions? RetrieveImagesOptions = null,
    [property: Description("Session states to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<LprSessionState>? SessionStates = null);

public sealed record ReportLprParkingInventoryFilters(
    [property: Description("Parking-zone GUIDs"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> ParkingZones,
    [property: Description("Occupancy aggregation granularity")] LprParkingOccupancyGranularity? Granularity = null,
    [property: Description("Maximum rows in each result batch"), Range(1, 100000)] int? MaximumRowsPerBatch = null,
    [property: Description("Image retrieval mode")] LprRetrieveImagesOptions? RetrieveImagesOptions = null);
