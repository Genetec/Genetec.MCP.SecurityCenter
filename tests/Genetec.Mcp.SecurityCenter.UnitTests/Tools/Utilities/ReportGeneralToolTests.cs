using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class ReportGeneralToolTests
{
    private const string TimeStart = "2024-01-01T00:00:00Z";
    private const string TimeEnd = "2024-01-02T00:00:00Z";
    private static readonly Guid EntityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Theory]
    [InlineData(GeneralReportType.AuditTrails)]
    [InlineData(GeneralReportType.Incident)]
    [InlineData(GeneralReportType.HealthStatistics)]
    [InlineData(GeneralReportType.UserGroup)]
    public void Request_DefaultLimits_AllowReportSpecificDefaults(GeneralReportType reportType)
    {
        var json = JsonSerializer.Serialize(new { report_type = reportType }, NumericStringConverter.ToolOptions);
        var request = JsonSerializer.Deserialize<ReportGeneralRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Null(request.MaximumResultCount);
        Assert.Null(request.MaximumSourceCount);
    }

    [Theory]
    [InlineData(GeneralReportType.AuditTrails)]
    [InlineData(GeneralReportType.Incident)]
    [InlineData(GeneralReportType.UserGroup)]
    public async Task MaximumSourceCount_ForOtherReport_IsRejected(GeneralReportType reportType)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => ReportGeneralTool.Execute(TestHelpers.FakeManager(),
            new ReportGeneralRequest(reportType, TimeStart: TimeStart, TimeEnd: TimeEnd, MaximumSourceCount: 1)));
        Assert.Equal("maximum_source_count", exception.ParamName);
    }

    [Fact]
    public async Task HealthStatistics_RejectsExplicitResultCount()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => ReportGeneralTool.Execute(TestHelpers.FakeManager(),
            new ReportGeneralRequest(GeneralReportType.HealthStatistics, TimeStart: TimeStart, TimeEnd: TimeEnd, MaximumResultCount: 50)));
        Assert.Equal("maximum_result_count", exception.ParamName);
        Assert.Contains("maximum_source_count", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public void MaximumSourceCount_ProductionBinding_RejectsInvalidValues(int sourceCount)
        => Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ReportGeneralRequest>(
            $$"""{"report_type":"health_statistics","maximum_source_count":{{sourceCount}}}""", NumericStringConverter.ToolOptions));

    [Theory]
    [InlineData(1)]
    [InlineData(100000)]
    public void MaximumSourceCount_ProductionBinding_AcceptsThresholdBoundaries(int sourceCount)
    {
        var request = JsonSerializer.Deserialize<ReportGeneralRequest>(
            $$"""{"report_type":"health_statistics","maximum_source_count":{{sourceCount}}}""", NumericStringConverter.ToolOptions)!;
        Assert.Equal(sourceCount, request.MaximumSourceCount);
        Assert.Null(request.MaximumResultCount);
    }

    [Fact]
    public void Discovery_HealthSourceCount_DescribesNativeSelectionThreshold()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "report_general", Detail: ToolDetailLevel.Full)), NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), tool => tool.GetProperty("name").GetString() == "report_general");
        var fields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        var sourceCount = Assert.Single(fields.EnumerateArray(), field => field.GetProperty("name").GetString() == "maximum_source_count");
        Assert.Contains("not a row limit", sourceCount.GetProperty("description").GetString());
        Assert.Contains("TooManyResults", sourceCount.GetProperty("description").GetString());
        Assert.Contains("Partial", sourceCount.GetProperty("description").GetString());
        Assert.Equal(1, sourceCount.GetProperty("min").GetInt32());
        Assert.Equal(100000, sourceCount.GetProperty("max").GetInt32());
    }

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

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(GeneralReportType.AuditTrails)]
    [InlineData(GeneralReportType.Incident)]
    [InlineData(GeneralReportType.HealthStatistics)]
    public async Task TimeReport_MissingTimeStart_ThrowsArgumentException(GeneralReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportGeneralTool.Execute(TestHelpers.FakeManager(),
                new ReportGeneralRequest(reportType, TimeEnd: TimeEnd)));
        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GeneralReportType.AuditTrails)]
    [InlineData(GeneralReportType.Incident)]
    [InlineData(GeneralReportType.HealthStatistics)]
    public async Task TimeReport_MissingTimeEnd_ThrowsArgumentException(GeneralReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportGeneralTool.Execute(TestHelpers.FakeManager(),
                new ReportGeneralRequest(reportType, TimeStart: TimeStart)));

        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public void MaximumResultCount_IsBounded(int maximumResultCount)
    {
        var request = new ReportGeneralRequest(GeneralReportType.UserGroup, MaximumResultCount: maximumResultCount);
        var validationResults = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(ReportGeneralRequest.MaximumResultCount)));
    }

    [Fact]
    public async Task AuditTrailsFilters_ForOtherReport_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportGeneralTool.Execute(TestHelpers.FakeManager(),
                new ReportGeneralRequest(GeneralReportType.Incident,
                    TimeStart: TimeStart, TimeEnd: TimeEnd,
                    Filters: new ReportGeneralFilters(AuditTrails: new AuditTrailsReportFilters()))));

        Assert.Contains("filters.audit_trails", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserGroupFilters_ForOtherReport_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportGeneralTool.Execute(TestHelpers.FakeManager(),
                new ReportGeneralRequest(GeneralReportType.HealthStatistics,
                    TimeStart: TimeStart, TimeEnd: TimeEnd,
                    Filters: new ReportGeneralFilters(UserGroup: new UserGroupReportFilters()))));

        Assert.Contains("filters.user_group", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IncidentTimeRange_WithOnlyOneBoundary_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportGeneralTool.Execute(TestHelpers.FakeManager(),
                new ReportGeneralRequest(GeneralReportType.Incident,
                    TimeStart: TimeStart, TimeEnd: TimeEnd,
                    Filters: new ReportGeneralFilters(Incident: new IncidentReportFilters(
                        CreationTimeRange: new ReportTimeRangeFilter(Start: TimeStart))))));

        Assert.Contains("filters.incident.creation_time_range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportFilters_ExposeTypedNestedSchemaAndRejectEmptyLists()
    {
        Assert.Equal(typeof(ReportGeneralFilters), typeof(ReportGeneralRequest).GetProperty(nameof(ReportGeneralRequest.Filters))!.PropertyType);
        Assert.Equal(typeof(List<Guid>), typeof(AuditTrailsReportFilters).GetProperty(nameof(AuditTrailsReportFilters.QueryEntities))!.PropertyType);
        Assert.Equal(typeof(List<GeneralApplicationType>), typeof(AuditTrailsReportFilters).GetProperty(nameof(AuditTrailsReportFilters.Applications))!.PropertyType);
        Assert.Equal(typeof(EntitySharingCriteriaFilter), typeof(UserGroupReportFilters).GetProperty(nameof(UserGroupReportFilters.EntitySharingCriteria))!.PropertyType);
        var queryEntitiesProperty = typeof(AuditTrailsReportFilters).GetProperty(nameof(AuditTrailsReportFilters.QueryEntities))!;
        Assert.NotNull(queryEntitiesProperty
            .GetCustomAttributes(typeof(MinLengthAttribute), inherit: true).Cast<MinLengthAttribute>().Single());
        var emptyListValidation = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(
            new List<Guid>(),
            new ValidationContext(new AuditTrailsReportFilters()) { MemberName = nameof(AuditTrailsReportFilters.QueryEntities) },
            emptyListValidation));
    }

    // ── Typed Web SDK filters ────────────────────────────────────────

    [Fact]
    public async Task AuditTrails_UsesTypedGuidAndEnumFilters()
    {
        var handler = new CapturingHandler();

        await ReportGeneralTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportGeneralRequest(
                GeneralReportType.AuditTrails,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 25,
                Filters: new ReportGeneralFilters(
                    SortOrder: ReportSortOrder.Descending,
                    AuditTrails: new AuditTrailsReportFilters(
                        QueryEntities: [EntityId],
                        Users: [EntityId],
                        EntityTypes: [ReportEntityType.Camera],
                        IncludedAreas: [EntityId],
                        ExcludedAreas: [EntityId],
                        AuditAttributes: [EntityId],
                        Applications: [GeneralApplicationType.SecurityDesk]))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/AuditTrailsReport?q=TimeRange.SetTimeRange({TimeStart},{TimeEnd}),MaximumResultCount=25," +
            $"SortOrder=Descending,QueryEntities@{EntityId},Users@{EntityId},EntityTypes@Camera," +
            $"IncludedAreas@{EntityId},ExcludedAreas@{EntityId},AuditAttributes@{EntityId},Applications@SecurityDesk",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Incident_UsesTypedNestedTimeRangesAndSearchMode()
    {
        var handler = new CapturingHandler();

        await ReportGeneralTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportGeneralRequest(
                GeneralReportType.Incident,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumResultCount: 25,
                Filters: new ReportGeneralFilters(
                    Incident: new IncidentReportFilters(
                        AlarmInstance: 7,
                        EventType: 42,
                        Guids: [EntityId],
                        CreationTimeRange: new ReportTimeRangeFilter(TimeStart, TimeEnd),
                        IncidentTimeRange: new ReportTimeRangeFilter(TimeStart, TimeEnd),
                        ModificationTimeRange: new ReportTimeRangeFilter(TimeStart, TimeEnd),
                        Notes: "door changed",
                        NotesSearchMode: ReportStringSearchMode.Contains,
                        ReferenceGuids: [EntityId]))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/Incident?q=TimeRange.SetTimeRange({TimeStart},{TimeEnd}),MaximumResultCount=25," +
            $"AlarmInstance=7,EventType=42,Guids@{EntityId}," +
            $"CreationTimeRange.SetTimeRange({TimeStart},{TimeEnd})," +
            $"IncidentTimeRange.SetTimeRange({TimeStart},{TimeEnd})," +
            $"ModificationTimeRange.SetTimeRange({TimeStart},{TimeEnd}),Notes=door changed," +
            $"NotesSearchMode=Contains,ReferenceGuids@{EntityId}",
            query,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(50)]
    public async Task HealthStatistics_UsesTypedGuidCollections(int? sourceCount)
    {
        var handler = new CapturingHandler();

        await ReportGeneralTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportGeneralRequest(
                GeneralReportType.HealthStatistics,
                TimeStart: TimeStart,
                TimeEnd: TimeEnd,
                MaximumSourceCount: sourceCount,
                Filters: new ReportGeneralFilters(
                    HealthStatistics: new HealthStatisticsReportFilters(
                        Sources: [EntityId],
                        ObserverEntities: [EntityId]))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/HealthStatistics?q=TimeRange.SetTimeRange({TimeStart},{TimeEnd}),MaximumResultCount={sourceCount ?? 50}," +
            $"Sources@{EntityId},ObserverEntities@{EntityId}",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserGroup_UsesTypedNestedSharingAndEntityFilters()
    {
        var handler = new CapturingHandler();

        await ReportGeneralTool.Execute(
            TestHelpers.FakeManager(handler),
            new ReportGeneralRequest(
                GeneralReportType.UserGroup,
                MaximumResultCount: 25,
                Filters: new ReportGeneralFilters(
                    SortOrder: ReportSortOrder.Ascending,
                    UserGroup: new UserGroupReportFilters(
                        Status: [GeneralUserStatus.Activated],
                        UserGroupIds: [EntityId],
                        Email: "ops@example.com",
                        EmailSearchMode: ReportStringSearchMode.Contains,
                        EntitySharingCriteria: new EntitySharingCriteriaFilter(true, false, true),
                        GroupRecursive: true,
                        SecurityLevelLowerBound: 2,
                        SecurityLevelLowerBoundOperator: GeneralFieldRangeType.Greaterequal,
                        SecurityLevelUpperBound: 9,
                        SecurityLevelUpperBoundOperator: GeneralFieldRangeType.Lowerequal,
                        Description: "operations",
                        DescriptionSearchMode: ReportStringSearchMode.StartsWith,
                        LogicalId: 15,
                        Name: "operators",
                        NameSearchMode: ReportStringSearchMode.Contains,
                        Owner: EntityId,
                        Page: 2,
                        PageSize: 10,
                        StrictResults: true,
                        DownloadAllRelatedData: false,
                        GuidsOnly: false,
                        CustomFields: [new ReportCustomFieldFilter(EntityId, TextValue: "billing")] ))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains(
            $"report/UserGroupReport?q=MaximumResultCount=25,SortOrder=Ascending,Status@Activated," +
            $"UserGroupIds@{EntityId},Email=ops\\@example.com,EmailSearchMode=Contains," +
            "EntitySharingCriteria.Shared=True,EntitySharingCriteria.SharedOverride=False," +
            "EntitySharingCriteria.SharedOverrideSearch=True,GroupRecursive=True," +
            "SecurityLevelLowerBound=2,SecurityLevelLowerBoundOperator=Greaterequal," +
            "SecurityLevelUpperBound=9,SecurityLevelUpperBoundOperator=Lowerequal,Description=operations," +
            "DescriptionSearchMode=StartsWith,DownloadAllRelatedData=False,LogicalId=15,Name=operators," +
            $"NameSearchMode=Contains,Owner={EntityId},StrictResults=True,GuidsOnly=False," +
            $"CustomFields@CustomField({EntityId},billing),PageSize=10,Page=2",
            query,
            StringComparison.Ordinal);
    }
}
