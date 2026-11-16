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
public class ReportGeneralTool
{
    [McpServerTool(
        Name = "report_general",
        Title = "General Reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description("Run general Security Center reports: audit_trails, incident, health_statistics, user_group. Use small result limits; do not fetch all entities automatically. Ask the user before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("General report request")] ReportGeneralRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateFilterReport(request);
        if (request.ReportType == GeneralReportType.HealthStatistics && request.MaximumResultCount is not null)
            throw new ArgumentException("maximum_result_count is not supported for health_statistics; use maximum_source_count.", "maximum_result_count");
        if (request.ReportType != GeneralReportType.HealthStatistics && request.MaximumSourceCount is not null)
            throw new ArgumentException("maximum_source_count is only supported for health_statistics.", "maximum_source_count");

        var client = sessionManager.GetClient(cancellationToken);

        switch (request.ReportType)
        {
            case GeneralReportType.AuditTrails:
                RequireTimeRange(request, "audit_trails");
                return await client.RunReport("AuditTrailsReport",
                    BuildQuery(
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        QueryValue("MaximumResultCount", request.MaximumResultCount ?? 50),
                        QueryValue("SortOrder", request.Filters?.SortOrder),
                        QueryCollection("QueryEntities", request.Filters?.AuditTrails?.QueryEntities),
                        QueryCollection("Users", request.Filters?.AuditTrails?.Users),
                        QueryCollection("EntityTypes", request.Filters?.AuditTrails?.EntityTypes),
                        QueryCollection("IncludedAreas", request.Filters?.AuditTrails?.IncludedAreas),
                        QueryCollection("ExcludedAreas", request.Filters?.AuditTrails?.ExcludedAreas),
                        QueryCollection("AuditAttributes", request.Filters?.AuditTrails?.AuditAttributes),
                        QueryCollection("Applications", request.Filters?.AuditTrails?.Applications)));

            case GeneralReportType.Incident:
                RequireTimeRange(request, "incident");
                var incidentFilters = request.Filters?.Incident;
                return await client.RunReport("Incident",
                    BuildQuery(
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        QueryValue("MaximumResultCount", request.MaximumResultCount ?? 50),
                        QueryValue("SortOrder", request.Filters?.SortOrder),
                        QueryValue("AlarmInstance", incidentFilters?.AlarmInstance),
                        QueryValue("EventType", incidentFilters?.EventType),
                        QueryCollection("Guids", incidentFilters?.Guids),
                        QueryTimeRange(incidentFilters?.CreationTimeRange?.Start, incidentFilters?.CreationTimeRange?.End, "CreationTimeRange"),
                        QueryTimeRange(incidentFilters?.IncidentTimeRange?.Start, incidentFilters?.IncidentTimeRange?.End, "IncidentTimeRange"),
                        QueryTimeRange(incidentFilters?.ModificationTimeRange?.Start, incidentFilters?.ModificationTimeRange?.End, "ModificationTimeRange"),
                        QueryValue("Notes", incidentFilters?.Notes),
                        QueryValue("NotesSearchMode", incidentFilters?.NotesSearchMode),
                        QueryCollection("ReferenceGuids", incidentFilters?.ReferenceGuids)));

            case GeneralReportType.HealthStatistics:
                RequireTimeRange(request, "health_statistics");
                return await client.RunReport("HealthStatistics",
                    BuildQuery(
                        QueryTimeRange(request.TimeStart, request.TimeEnd),
                        QueryValue("MaximumResultCount", request.MaximumSourceCount ?? 50),
                        QueryValue("SortOrder", request.Filters?.SortOrder),
                        QueryCollection("Sources", request.Filters?.HealthStatistics?.Sources),
                        QueryCollection("ObserverEntities", request.Filters?.HealthStatistics?.ObserverEntities)));

            case GeneralReportType.UserGroup:
                if (request.TimeStart is not null || request.TimeEnd is not null)
                    throw new ArgumentException("Parameters 'time_start' and 'time_end' are not supported for user_group.");

                var userGroupFilters = request.Filters?.UserGroup;
                return await client.RunPagedReport("UserGroupReport",
                    BuildQuery(
                        QueryValue("MaximumResultCount", request.MaximumResultCount ?? 50),
                        QueryValue("SortOrder", request.Filters?.SortOrder),
                        QueryCollection("Status", userGroupFilters?.Status),
                        QueryCollection("UserGroupIds", userGroupFilters?.UserGroupIds),
                        QueryValue("Email", userGroupFilters?.Email),
                        QueryValue("EmailSearchMode", userGroupFilters?.EmailSearchMode),
                        QueryValue("EntitySharingCriteria.Shared", userGroupFilters?.EntitySharingCriteria?.Shared),
                        QueryValue("EntitySharingCriteria.SharedOverride", userGroupFilters?.EntitySharingCriteria?.SharedOverride),
                        QueryValue("EntitySharingCriteria.SharedOverrideSearch", userGroupFilters?.EntitySharingCriteria?.SharedOverrideSearch),
                        QueryValue("GroupRecursive", userGroupFilters?.GroupRecursive),
                        QueryValue("SecurityLevelLowerBound", userGroupFilters?.SecurityLevelLowerBound),
                        QueryValue("SecurityLevelLowerBoundOperator", userGroupFilters?.SecurityLevelLowerBoundOperator),
                        QueryValue("SecurityLevelUpperBound", userGroupFilters?.SecurityLevelUpperBound),
                        QueryValue("SecurityLevelUpperBoundOperator", userGroupFilters?.SecurityLevelUpperBoundOperator),
                        QueryValue("Description", userGroupFilters?.Description),
                        QueryValue("DescriptionSearchMode", userGroupFilters?.DescriptionSearchMode),
                        QueryValue("DownloadAllRelatedData", userGroupFilters?.DownloadAllRelatedData),
                        QueryValue("LogicalId", userGroupFilters?.LogicalId),
                        QueryValue("Name", userGroupFilters?.Name),
                        QueryValue("NameSearchMode", userGroupFilters?.NameSearchMode),
                        QueryValue("Owner", userGroupFilters?.Owner),
                        QueryValue("StrictResults", userGroupFilters?.StrictResults),
                        QueryValue("GuidsOnly", userGroupFilters?.GuidsOnly),
                        ActivityReportQuery.CustomFields(userGroupFilters?.CustomFields)), userGroupFilters?.PageSize, userGroupFilters?.Page);

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }
    }

    private static void RequireTimeRange(ReportGeneralRequest request, string reportName)
    {
        if (request.TimeStart is null)
            throw new ArgumentException($"Parameter 'time_start' is required for {reportName}.");
        if (request.TimeEnd is null)
            throw new ArgumentException($"Parameter 'time_end' is required for {reportName}.");
    }

    private static void ValidateFilterReport(ReportGeneralRequest request)
    {
        var filters = request.Filters;
        if (filters?.AuditTrails is not null && request.ReportType != GeneralReportType.AuditTrails)
            throw new ArgumentException("Parameter 'filters.audit_trails' is only valid for audit_trails.");
        if (filters?.Incident is not null && request.ReportType != GeneralReportType.Incident)
            throw new ArgumentException("Parameter 'filters.incident' is only valid for incident.");
        if (filters?.HealthStatistics is not null && request.ReportType != GeneralReportType.HealthStatistics)
            throw new ArgumentException("Parameter 'filters.health_statistics' is only valid for health_statistics.");
        if (filters?.UserGroup is not null && request.ReportType != GeneralReportType.UserGroup)
            throw new ArgumentException("Parameter 'filters.user_group' is only valid for user_group.");

        ValidateTimeRange(filters?.Incident?.CreationTimeRange, "filters.incident.creation_time_range");
        ValidateTimeRange(filters?.Incident?.IncidentTimeRange, "filters.incident.incident_time_range");
        ValidateTimeRange(filters?.Incident?.ModificationTimeRange, "filters.incident.modification_time_range");
    }

    private static void ValidateTimeRange(ReportTimeRangeFilter? range, string name)
    {
        if (range is not null && (range.Start is null) != (range.End is null))
            throw new ArgumentException($"Parameters '{name}.start' and '{name}.end' must be provided together.");
    }
}

public enum GeneralReportType { AuditTrails, Incident, HealthStatistics, UserGroup }

public enum GeneralApplicationType
{
    None,
    ConfigTool,
    AccessManager,
    AccessDatastore,
    Sdk,
    SecurityDesk,
    IntegrationService,
    MobileServer,
    MobileServerSlave,
    WebClient,
    MobileClient,
    Federation,
    RoamingFederation,
    AccessDatastoreMacroManager,
    GcsRemote,
    SaasOperation,
    SaasConfiguration,
    WebConfiguration
}

public enum GeneralUserStatus { Activated, Deactivated }

public enum GeneralFieldRangeType { None, Equal, Lowerequal, Greaterequal, Different, Lower, Greater, Like }

public sealed record ReportGeneralRequest(
    [property: Description("Report type to run"), Required] GeneralReportType ReportType,
    [property: Description("Report start time ISO 8601; required for audit_trails, incident, and health_statistics"), Iso8601] string? TimeStart = null,
    [property: Description("Report end time ISO 8601; required for audit_trails, incident, and health_statistics"), Iso8601] string? TimeEnd = null,
    [property: Description("Maximum result selection threshold for audit_trails, incident, and user_group (default 50); not supported for health_statistics. Native responses can include a look-ahead row and an incomplete status."), Range(1, 100000)] int? MaximumResultCount = null,
    [property: Description("Typed report filters; provide only the filter group matching report_type")] ReportGeneralFilters? Filters = null,
    [property: Description("Maximum source selection threshold for health_statistics (default 50); not a row limit. The Web SDK can select one additional source and return multiple statistic rows per source. Native TooManyResults and Partial status are preserved."), Range(1, 100000)] int? MaximumSourceCount = null);

public sealed record ReportGeneralFilters(
    [property: Description("Common sorting filter for the selected report")] ReportSortOrder? SortOrder = null,
    [property: Description("Audit trail filters; valid only for audit_trails")] AuditTrailsReportFilters? AuditTrails = null,
    [property: Description("Incident filters; valid only for incident")] IncidentReportFilters? Incident = null,
    [property: Description("Health statistics filters; valid only for health_statistics")] HealthStatisticsReportFilters? HealthStatistics = null,
    [property: Description("User group filters; valid only for user_group")] UserGroupReportFilters? UserGroup = null);

public sealed record AuditTrailsReportFilters(
    [property: Description("Entity GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? QueryEntities = null,
    [property: Description("User or role GUIDs whose audit entries should be included"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Users = null,
    [property: Description("Entity types to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportEntityType>? EntityTypes = null,
    [property: Description("Area GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? IncludedAreas = null,
    [property: Description("Area GUIDs to exclude"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ExcludedAreas = null,
    [property: Description("Audit attribute GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? AuditAttributes = null,
    [property: Description("Application types to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<GeneralApplicationType>? Applications = null);

public sealed record IncidentReportFilters(
    [property: Description("Alarm instance to include")] int? AlarmInstance = null,
    [property: Description("Event type identifier to include")] int? EventType = null,
    [property: Description("Incident GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Guids = null,
    [property: Description("Creation time range")] ReportTimeRangeFilter? CreationTimeRange = null,
    [property: Description("Incident time range")] ReportTimeRangeFilter? IncidentTimeRange = null,
    [property: Description("Modification time range")] ReportTimeRangeFilter? ModificationTimeRange = null,
    [property: Description("Incident notes text filter")] string? Notes = null,
    [property: Description("Search mode for notes")] ReportStringSearchMode? NotesSearchMode = null,
    [property: Description("Reference GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ReferenceGuids = null);

public sealed record ReportTimeRangeFilter(
    [property: Description("Range start time ISO 8601"), Iso8601] string? Start = null,
    [property: Description("Range end time ISO 8601"), Iso8601] string? End = null);

public sealed record HealthStatisticsReportFilters(
    [property: Description("Source entity GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? Sources = null,
    [property: Description("Observer entity GUIDs to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? ObserverEntities = null);

public sealed record UserGroupReportFilters(
    [property: Description("User account statuses to include"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<GeneralUserStatus>? Status = null,
    [property: Description("User group GUIDs to search within"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid>? UserGroupIds = null,
    [property: Description("Email address text filter")] string? Email = null,
    [property: Description("Search mode for email")] ReportStringSearchMode? EmailSearchMode = null,
    [property: Description("Entity sharing criteria")]
    EntitySharingCriteriaFilter? EntitySharingCriteria = null,
    [property: Description("Whether to search user groups recursively")] bool? GroupRecursive = null,
    [property: Description("Lower security level bound")] int? SecurityLevelLowerBound = null,
    [property: Description("Comparison for the lower security level bound")] GeneralFieldRangeType? SecurityLevelLowerBoundOperator = null,
    [property: Description("Upper security level bound")] int? SecurityLevelUpperBound = null,
    [property: Description("Comparison for the upper security level bound")] GeneralFieldRangeType? SecurityLevelUpperBoundOperator = null,
    [property: Description("Entity description text filter")] string? Description = null,
    [property: Description("Search mode for description")] ReportStringSearchMode? DescriptionSearchMode = null,
    [property: Description("Logical identifier of the entity")] int? LogicalId = null,
    [property: Description("Entity name text filter")] string? Name = null,
    [property: Description("Search mode for name")] ReportStringSearchMode? NameSearchMode = null,
    [property: Description("Owner entity GUID")] Guid? Owner = null,
    [property: Description("Page number to return"), Range(1, int.MaxValue)] int? Page = null,
    [property: Description("Page size for each user_group page (range 1-100)"), Range(1, ResourceLimits.MaxPageSize)] int? PageSize = null,
    [property: Description("Require results to match the query strictly")] bool? StrictResults = null,
    [property: Description("Return only matching GUIDs")] bool? GuidsOnly = null,
    [property: Description("Return related entity data for matching user groups")] bool? DownloadAllRelatedData = null,
    [property: Description("Custom field filters supported by user_group"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<ReportCustomFieldFilter>? CustomFields = null);

public sealed record EntitySharingCriteriaFilter(
    [property: Description("Whether the user group is shared")] bool? Shared = null,
    [property: Description("Whether shared override is enabled")] bool? SharedOverride = null,
    [property: Description("Whether to filter on shared override")] bool? SharedOverrideSearch = null);
