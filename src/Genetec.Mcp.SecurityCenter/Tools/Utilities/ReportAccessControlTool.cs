using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Validation;
using ModelContextProtocol.Server;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class ReportAccessControlTool
{
    [McpServerTool(
        Name = "report_access_control",
        Title = "Access Control Reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Run access control reports: zone_activity, elevator_activity, acu_activity, time_attendance, " +
                 "access_rule_configuration, unused_credentials, unused_cardholders, intrusion_activity. " +
                 "intrusion_activity requires an intrusion area id and a time range. Use small result limits; do not fetch all entities automatically. Ask the user before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Access control report request")] ReportAccessControlRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTopLevelFilters(request);
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.ReportType)
        {
            case AccessControlReportType.ZoneActivity:
                RequireEntityAndTimeRange(request, "zone_activity");
                ValidateFilterGroup(request.Filters, AccessControlReportType.ZoneActivity);
                var zoneFilters = request.Filters?.ZoneActivity;
                return await client.RunReport("ZoneActivity", BuildQuery(
                    QueryCollection("Zones", EntityIds(request.Id, zoneFilters?.ZoneIds)),
                    BuildActivityFilters(zoneFilters?.Activity),
                    QueryCollection("IncludedExpansionEntities", zoneFilters?.Activity?.IncludedExpansionEntities),
                    QueryCollection("ExcludedExpansionEntities", zoneFilters?.Activity?.ExcludedExpansionEntities),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            case AccessControlReportType.ElevatorActivity:
                RequireEntityAndTimeRange(request, "elevator_activity");
                ValidateFilterGroup(request.Filters, AccessControlReportType.ElevatorActivity);
                var elevatorFilters = request.Filters?.ElevatorActivity;
                return await client.RunReport("ElevatorActivity", BuildQuery(
                    QueryCollection("Elevators", EntityIds(request.Id, elevatorFilters?.ElevatorIds)),
                    BuildActivityFilters(elevatorFilters?.Activity),
                    QueryCollection("IncludedExpansionEntities", elevatorFilters?.Activity?.IncludedExpansionEntities),
                    QueryCollection("ExcludedExpansionEntities", elevatorFilters?.Activity?.ExcludedExpansionEntities),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            case AccessControlReportType.AcuActivity:
                RequireEntityAndTimeRange(request, "acu_activity");
                ValidateFilterGroup(request.Filters, AccessControlReportType.AcuActivity);
                var acuFilters = request.Filters?.AcuActivity;
                return await client.RunReport("AccessControlUnitActivity", BuildQuery(
                    QueryCollection("Units", EntityIds(request.Id, acuFilters?.UnitIds)),
                    BuildActivityFilters(acuFilters?.Activity),
                    QueryCollection("IncludedExpansionEntities", acuFilters?.Activity?.IncludedExpansionEntities),
                    QueryCollection("ExcludedExpansionEntities", acuFilters?.Activity?.ExcludedExpansionEntities),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            case AccessControlReportType.TimeAttendance:
                RequireTimeRange(request, "time_attendance");
                ValidateFilterGroup(request.Filters, AccessControlReportType.TimeAttendance);
                var timeAttendanceFilters = request.Filters?.TimeAttendance;
                return await client.RunReport("TimeAttendanceActivity", BuildQuery(
                    QueryCollection("Areas", timeAttendanceFilters?.Areas),
                    QueryCollection("Cardholders", timeAttendanceFilters?.Cardholders),
                    QueryValue("DayStartOffset", timeAttendanceFilters?.DayStartOffset),
                    QueryValue("SortOrder", timeAttendanceFilters?.SortOrder),
                    ActivityReportQuery.CustomFields(timeAttendanceFilters?.CustomFields),
                    QueryTimeRange(request.TimeStart, request.TimeEnd)));

            case AccessControlReportType.AccessRuleConfiguration:
                ValidateFilterGroup(request.Filters, AccessControlReportType.AccessRuleConfiguration);
                var accessRuleFilters = request.Filters?.AccessRuleConfiguration;
                return await client.RunPagedReport("AccessRuleConfiguration", BuildQuery(
                    QueryValue("SortOrder", accessRuleFilters?.SortOrder),
                    QueryValue("AccessRuleType", accessRuleFilters?.AccessRuleType),
                    QueryValue("Name", accessRuleFilters?.Name),
                    QueryValue("NameSearchMode", accessRuleFilters?.NameSearchMode),
                    QueryValue("Description", accessRuleFilters?.Description),
                    QueryValue("DescriptionSearchMode", accessRuleFilters?.DescriptionSearchMode),
                    QueryValue("LogicalId", accessRuleFilters?.LogicalId),
                    QueryValue("Owner", accessRuleFilters?.Owner),
                    QueryValue("StrictResults", accessRuleFilters?.StrictResults),
                    QueryValue("GuidsOnly", accessRuleFilters?.GuidsOnly),
                    QueryValue("DownloadAllRelatedData", accessRuleFilters?.DownloadAllRelatedData),
                    ActivityReportQuery.CustomFields(accessRuleFilters?.CustomFields)), request.PageSize, request.Page);

            case AccessControlReportType.UnusedCredentials:
                RequireTimeRange(request, "unused_credentials");
                ValidateFilterGroup(request.Filters, AccessControlReportType.UnusedCredentials);
                return await client.RunReport("UnusedCredentials", BuildQuery(
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            case AccessControlReportType.UnusedCardholders:
                RequireTimeRange(request, "unused_cardholders");
                ValidateFilterGroup(request.Filters, AccessControlReportType.UnusedCardholders);
                return await client.RunReport("UnusedCardholders", BuildQuery(
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            case AccessControlReportType.IntrusionActivity:
                RequireEntityAndTimeRange(request, "intrusion_activity");
                ValidateFilterGroup(request.Filters, AccessControlReportType.IntrusionActivity);
                var intrusionFilters = request.Filters?.IntrusionActivity;
                return await client.RunReport("IntrusionAreaActivity", BuildQuery(
                    QueryCollection("IntrusionAreas", EntityIds(request.Id, intrusionFilters?.IntrusionAreaIds)),
                    BuildActivityFilters(intrusionFilters?.Activity),
                    QueryCollection("IncludedExpansionEntities", intrusionFilters?.Activity?.IncludedExpansionEntities),
                    QueryCollection("ExcludedExpansionEntities", intrusionFilters?.Activity?.ExcludedExpansionEntities),
                    QueryTimeRange(request.TimeStart, request.TimeEnd),
                    QueryValue("MaximumResultCount", MaximumResultCount(request))));

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }
    }

    private static string BuildActivityFilters(AccessControlActivityReportFilters? filters) => BuildQuery(
        QueryCollection("Events", filters?.Events),
        ActivityReportQuery.CustomFields(filters?.CustomFields),
        QueryCollection("CustomEvents", filters?.CustomEvents),
        QueryValue("SortOrder", filters?.SortOrder),
        QueryCollection("OfflinePeriodTypes", filters?.OfflinePeriodTypes),
        QueryCollection("AccessDecisionAuthorities", filters?.AccessDecisionAuthorities),
        QueryValue("UseDeviceTimeZone", filters?.UseDeviceTimeZone));

    private static IReadOnlyCollection<Guid> EntityIds(Guid? id, List<Guid>? ids)
    {
        var entityIds = new List<Guid>();
        if (id.HasValue)
            entityIds.Add(id.Value);
        if (ids is not null)
            entityIds.AddRange(ids);
        return entityIds.Distinct().ToArray();
    }

    private static void RequireEntityAndTimeRange(ReportAccessControlRequest request, string reportName)
    {
        if (request.Id is null)
            throw new ArgumentException($"Parameter 'id' is required for {reportName}.");
        RequireTimeRange(request, reportName);
    }

    private static int MaximumResultCount(ReportAccessControlRequest request) => request.MaximumResultCount ?? 10;

    private static void ValidateTopLevelFilters(ReportAccessControlRequest request)
    {
        var reportUsesEntityId = request.ReportType is
            AccessControlReportType.ZoneActivity or AccessControlReportType.ElevatorActivity or
            AccessControlReportType.AcuActivity or AccessControlReportType.IntrusionActivity;
        if (!reportUsesEntityId && request.Id is not null)
            throw new ArgumentException($"Parameter 'id' is not supported for {request.ReportType}.");

        if (request.ReportType == AccessControlReportType.AccessRuleConfiguration &&
            (request.TimeStart is not null || request.TimeEnd is not null))
            throw new ArgumentException("time_start and time_end are not supported for access_rule_configuration.");

        if (request.ReportType != AccessControlReportType.AccessRuleConfiguration &&
            (request.PageSize != 50 || request.Page != 1))
            throw new ArgumentException($"page_size and page are only supported for access_rule_configuration.");

        if (request.ReportType is AccessControlReportType.TimeAttendance or AccessControlReportType.AccessRuleConfiguration)
            RejectUnsupportedMaximumResultCount(request, request.ReportType.ToString());
    }

    private static void RejectUnsupportedMaximumResultCount(ReportAccessControlRequest request, string reportName)
    {
        if (request.MaximumResultCount is not null)
            throw new ArgumentException($"Parameter 'maximum_result_count' is not supported for {reportName}.");
    }

    private static void RequireTimeRange(ReportAccessControlRequest request, string reportName)
    {
        if (request.TimeStart is null)
            throw new ArgumentException($"Parameter 'time_start' is required for {reportName}.");
        if (request.TimeEnd is null)
            throw new ArgumentException($"Parameter 'time_end' is required for {reportName}.");
    }

    private static void ValidateFilterGroup(ReportAccessControlFilters? filters, AccessControlReportType reportType)
    {
        if (filters is null)
            return;

        var hasWrongFilterGroup = reportType switch
        {
            AccessControlReportType.ZoneActivity =>
                filters.ElevatorActivity is not null || filters.AcuActivity is not null ||
                filters.TimeAttendance is not null || filters.AccessRuleConfiguration is not null ||
                filters.IntrusionActivity is not null,
            AccessControlReportType.ElevatorActivity =>
                filters.ZoneActivity is not null || filters.AcuActivity is not null ||
                filters.TimeAttendance is not null || filters.AccessRuleConfiguration is not null ||
                filters.IntrusionActivity is not null,
            AccessControlReportType.AcuActivity =>
                filters.ZoneActivity is not null || filters.ElevatorActivity is not null ||
                filters.TimeAttendance is not null || filters.AccessRuleConfiguration is not null ||
                filters.IntrusionActivity is not null,
            AccessControlReportType.TimeAttendance =>
                filters.ZoneActivity is not null || filters.ElevatorActivity is not null ||
                filters.AcuActivity is not null || filters.AccessRuleConfiguration is not null ||
                filters.IntrusionActivity is not null,
            AccessControlReportType.AccessRuleConfiguration =>
                filters.ZoneActivity is not null || filters.ElevatorActivity is not null ||
                filters.AcuActivity is not null || filters.TimeAttendance is not null ||
                filters.IntrusionActivity is not null,
            AccessControlReportType.IntrusionActivity =>
                filters.ZoneActivity is not null || filters.ElevatorActivity is not null ||
                filters.AcuActivity is not null || filters.TimeAttendance is not null ||
                filters.AccessRuleConfiguration is not null,
            AccessControlReportType.UnusedCredentials or AccessControlReportType.UnusedCardholders =>
                filters.ZoneActivity is not null || filters.ElevatorActivity is not null ||
                filters.AcuActivity is not null || filters.TimeAttendance is not null ||
                filters.AccessRuleConfiguration is not null || filters.IntrusionActivity is not null,
            _ => true
        };

        if (hasWrongFilterGroup)
            throw new ArgumentException($"Filter group does not match report type '{reportType}'.");
    }
}

public enum AccessControlReportType
{
    ZoneActivity,
    ElevatorActivity,
    AcuActivity,
    TimeAttendance,
    AccessRuleConfiguration,
    UnusedCredentials,
    UnusedCardholders,
    IntrusionActivity
}

public enum AccessControlOfflinePeriodType
{
    None,
    GracePeriod,
    OfflineAlarmPeriod,
    Offline
}

public enum AccessControlDecisionAuthority
{
    None,
    AccessManager,
    Softwire,
    IntelligentController
}

public enum AccessRuleTypeFilter
{
    Permanent,
    Temporary
}

public sealed record ReportAccessControlRequest(
    [property: Description("Report type to run"), Required] AccessControlReportType ReportType,
    [property: Description("Entity GUID to filter by; required for zone_activity, elevator_activity, acu_activity, and intrusion_activity")] Guid? Id = null,
    [property: Description("Report start time ISO 8601; required for reports with a time range"), Iso8601] string? TimeStart = null,
    [property: Description("Report end time ISO 8601; required for reports with a time range"), Iso8601] string? TimeEnd = null,
    [property: Description("Maximum results for activity and unused reports (default 10). Unsupported by time_attendance and access_rule_configuration."), Range(1, 100000)] int? MaximumResultCount = null,
    [property: Description("Maximum access_rule_configuration results per page (default 50, range 1-100); use small pages and ask the user before large queries"), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Access rule configuration page number (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("Typed report-specific filters; fields are documented on ReportAccessControlFilters")] ReportAccessControlFilters? Filters = null);

public sealed record ReportAccessControlFilters(
    [property: Description("Zone activity filters; provide only when report_type is zone_activity")] ZoneActivityReportFilters? ZoneActivity = null,
    [property: Description("Elevator activity filters; provide only when report_type is elevator_activity")] ElevatorActivityReportFilters? ElevatorActivity = null,
    [property: Description("Access control unit activity filters; provide only when report_type is acu_activity")] AcuActivityReportFilters? AcuActivity = null,
    [property: Description("Time attendance filters; provide only when report_type is time_attendance")] TimeAttendanceReportFilters? TimeAttendance = null,
    [property: Description("Access rule configuration filters; provide only when report_type is access_rule_configuration")] AccessRuleConfigurationReportFilters? AccessRuleConfiguration = null,
    [property: Description("Intrusion area activity filters; provide only when report_type is intrusion_activity")] IntrusionActivityReportFilters? IntrusionActivity = null);

public sealed record ZoneActivityReportFilters(
    [property: Description("Additional zone GUIDs to include with id"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ZoneIds = null,
    [property: Description("SDK activity filters for this report")] AccessControlActivityReportFilters? Activity = null);

public sealed record ElevatorActivityReportFilters(
    [property: Description("Additional elevator GUIDs to include with id"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ElevatorIds = null,
    [property: Description("SDK activity filters for this report")] AccessControlActivityReportFilters? Activity = null);

public sealed record AcuActivityReportFilters(
    [property: Description("Additional access control unit GUIDs to include with id"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? UnitIds = null,
    [property: Description("SDK activity filters for this report")] AccessControlActivityReportFilters? Activity = null);

public sealed record IntrusionActivityReportFilters(
    [property: Description("Additional intrusion area GUIDs to include with id"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? IntrusionAreaIds = null,
    [property: Description("SDK activity filters for this report")] AccessControlActivityReportFilters? Activity = null);

public sealed record AccessControlActivityReportFilters(
    [property: Description("Event types to include"), MaxLength(ResourceLimits.MaxBatchItems)] List<ScEventType>? Events = null,
    [property: Description("Custom event IDs to include"), MaxLength(ResourceLimits.MaxBatchItems)] List<int>? CustomEvents = null,
    [property: Description("Result order")] ReportSortOrder? SortOrder = null,
    [property: Description("Custom field filters supported by activity reports"), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportCustomFieldFilter>? CustomFields = null,
    [property: Description("Offline period types to include"), MaxLength(ResourceLimits.MaxBatchItems)] List<AccessControlOfflinePeriodType>? OfflinePeriodTypes = null,
    [property: Description("Components allowed to make the access decision"), MaxLength(ResourceLimits.MaxBatchItems)] List<AccessControlDecisionAuthority>? AccessDecisionAuthorities = null,
    [property: Description("Whether to use the device time zones")] bool? UseDeviceTimeZone = null,
    [property: Description("Expanded area or entity GUIDs to include"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? IncludedExpansionEntities = null,
    [property: Description("Expanded entity GUIDs to exclude"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ExcludedExpansionEntities = null);

public sealed record TimeAttendanceReportFilters(
    [property: Description("Area GUIDs to query"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Areas = null,
    [property: Description("Cardholder GUIDs to query"), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Cardholders = null,
    [property: Description("Local day start offset")] TimeSpan? DayStartOffset = null,
    [property: Description("Result order")] ReportSortOrder? SortOrder = null,
    [property: Description("Custom field filters supported by TimeAttendanceQuery"), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportCustomFieldFilter>? CustomFields = null);

public sealed record AccessRuleConfigurationReportFilters(
    [property: Description("Access rule type")] AccessRuleTypeFilter? AccessRuleType = null,
    [property: Description("Result order")] ReportSortOrder? SortOrder = null,
    [property: Description("Custom field filters supported by entity configuration reports"), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportCustomFieldFilter>? CustomFields = null,
    [property: Description("Entity name")] string? Name = null,
    [property: Description("Name search mode")] ReportStringSearchMode? NameSearchMode = null,
    [property: Description("Entity description")] string? Description = null,
    [property: Description("Description search mode")] ReportStringSearchMode? DescriptionSearchMode = null,
    [property: Description("Logical ID"), Range(1, int.MaxValue)] int? LogicalId = null,
    [property: Description("Owner GUID")] Guid? Owner = null,
    [property: Description("Require results to match all filters exactly")] bool? StrictResults = null,
    [property: Description("Return GUIDs only")] bool? GuidsOnly = null,
    [property: Description("Download related entities")] bool? DownloadAllRelatedData = null);
