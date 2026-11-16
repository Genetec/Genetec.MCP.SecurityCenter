using Genetec.Mcp.SecurityCenter.Tools;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class ReportAccessControlToolTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Theory]
    [InlineData(AccessControlReportType.ZoneActivity, "ZoneActivity", "Zones@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    [InlineData(AccessControlReportType.ElevatorActivity, "ElevatorActivity", "Elevators@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    [InlineData(AccessControlReportType.AcuActivity, "AccessControlUnitActivity", "Units@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    [InlineData(AccessControlReportType.TimeAttendance, "TimeAttendanceActivity", "TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)")]
    [InlineData(AccessControlReportType.AccessRuleConfiguration, "AccessRuleConfiguration", "PageSize=50,Page=1")]
    [InlineData(AccessControlReportType.UnusedCredentials, "UnusedCredentials", "TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    [InlineData(AccessControlReportType.UnusedCardholders, "UnusedCardholders", "TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    [InlineData(AccessControlReportType.IntrusionActivity, "IntrusionAreaActivity", "IntrusionAreas@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=10")]
    public async Task ReportTypes_UseExpectedWebSdkRouteAndQuery(
        AccessControlReportType reportType, string sdkReportType, string expectedQuery)
    {
        var handler = new CapturingHandler();

        var request = reportType switch
        {
            AccessControlReportType.ZoneActivity or
            AccessControlReportType.ElevatorActivity or
            AccessControlReportType.AcuActivity or
            AccessControlReportType.IntrusionActivity => new ReportAccessControlRequest(
                reportType, EntityId, "2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z"),
            AccessControlReportType.TimeAttendance => new ReportAccessControlRequest(
                reportType, TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"),
            AccessControlReportType.UnusedCredentials or
            AccessControlReportType.UnusedCardholders => new ReportAccessControlRequest(
                reportType, TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z"),
            AccessControlReportType.AccessRuleConfiguration => new ReportAccessControlRequest(reportType),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType))
        };

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            request);

        var capturedRequest = Assert.Single(handler.Requests);
        var uri = Uri.UnescapeDataString(capturedRequest.RequestUri!.ToString());
        Assert.Contains($"report/{sdkReportType}?q={expectedQuery}", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnusedReport_UsesTypedMaximumResultCount()
    {
        var handler = new CapturingHandler();

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            new ReportAccessControlRequest(
                AccessControlReportType.UnusedCredentials,
                TimeStart: "2020-01-01T00:00:00Z",
                TimeEnd: "2026-08-25T23:59:59Z",
                MaximumResultCount: 25));

        var request = Assert.Single(handler.Requests);
        var query = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Assert.Contains(
            "report/UnusedCredentials?q=TimeRange.SetTimeRange(2020-01-01T00:00:00Z,2026-08-25T23:59:59Z),MaximumResultCount=25",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivityReport_UsesTypedSdkFilters()
    {
        var handler = new CapturingHandler();

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            new ReportAccessControlRequest(
                AccessControlReportType.ZoneActivity,
                EntityId,
                "2024-01-01T00:00:00Z",
                "2024-01-02T00:00:00Z",
                MaximumResultCount: 25,
                Filters: new ReportAccessControlFilters(ZoneActivity: new ZoneActivityReportFilters(
                    ZoneIds: [Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")],
                    Activity: new AccessControlActivityReportFilters(
                        Events: [Genetec.Mcp.SecurityCenter.ScEventType.AccessGranted],
                        CustomEvents: [17],
                        SortOrder: ReportSortOrder.Descending,
                        CustomFields: [new ReportCustomFieldFilter(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"), TextValue: "Lobby")],
                        OfflinePeriodTypes: [AccessControlOfflinePeriodType.Offline],
                        AccessDecisionAuthorities: [AccessControlDecisionAuthority.AccessManager],
                        UseDeviceTimeZone: true)))));

        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains(
            "report/ZoneActivity?q=Zones@11111111-2222-3333-4444-555555555555@aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee," +
            "Events@AccessGranted,CustomFields@CustomField(bbbbbbbb-cccc-dddd-eeee-ffffffffffff,Lobby)," +
            "CustomEvents@17,SortOrder=Descending,OfflinePeriodTypes@Offline," +
            "AccessDecisionAuthorities@AccessManager,UseDeviceTimeZone=True," +
            "TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=25",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivityReport_CustomFieldValues_UseTypedRepresentationsAndEscaping()
    {
        var handler = new CapturingHandler();
        List<ReportCustomFieldFilter> customFields =
        [
            new ReportCustomFieldFilter(Guid.Parse("11111111-1111-1111-1111-111111111111"), TextValue: "West,(Lobby)"),
            new ReportCustomFieldFilter(Guid.Parse("22222222-2222-2222-2222-222222222222"), NumericValue: 42),
            new ReportCustomFieldFilter(Guid.Parse("33333333-3333-3333-3333-333333333333"), DecimalValue: 12.5),
            new ReportCustomFieldFilter(Guid.Parse("44444444-4444-4444-4444-444444444444"), BooleanValue: true),
            new ReportCustomFieldFilter(Guid.Parse("55555555-5555-5555-5555-555555555555"), DateValue: new DateOnly(2025, 1, 2)),
            new ReportCustomFieldFilter(Guid.Parse("66666666-6666-6666-6666-666666666666"), EntityValue: EntityId),
            new ReportCustomFieldFilter(Guid.Parse("77777777-7777-7777-7777-777777777777"))
        ];

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            new ReportAccessControlRequest(
                AccessControlReportType.ZoneActivity,
                EntityId,
                "2024-01-01T00:00:00Z",
                "2024-01-02T00:00:00Z",
                Filters: new ReportAccessControlFilters(ZoneActivity: new ZoneActivityReportFilters(
                    Activity: new AccessControlActivityReportFilters(CustomFields: customFields)))));

        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains(
            "CustomFields@CustomField(11111111-1111-1111-1111-111111111111,West\\,\\(Lobby\\))" +
            "@CustomField(22222222-2222-2222-2222-222222222222,42)" +
            "@CustomField(33333333-3333-3333-3333-333333333333,12.5)" +
            "@CustomField(44444444-4444-4444-4444-444444444444,True)" +
            "@CustomField(55555555-5555-5555-5555-555555555555,2025-01-02)" +
            $"@CustomField(66666666-6666-6666-6666-666666666666,{EntityId})" +
            "@CustomField(77777777-7777-7777-7777-777777777777)",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimeAttendance_UsesTypedSdkFilters()
    {
        var handler = new CapturingHandler();
        var areaId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var cardholderId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            new ReportAccessControlRequest(
                AccessControlReportType.TimeAttendance,
                TimeStart: "2024-01-01T00:00:00Z",
                TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportAccessControlFilters(
                    TimeAttendance: new TimeAttendanceReportFilters(
                        Areas: [areaId],
                        Cardholders: [cardholderId],
                        DayStartOffset: TimeSpan.FromHours(8),
                        SortOrder: ReportSortOrder.Ascending))));

        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains(
            $"report/TimeAttendanceActivity?q=Areas@{areaId},Cardholders@{cardholderId},DayStartOffset=08:00:00," +
            "SortOrder=Ascending,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z)",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilterForDifferentReport_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(
                    AccessControlReportType.ZoneActivity,
                    EntityId,
                    "2024-01-01T00:00:00Z",
                    "2024-01-02T00:00:00Z",
                    Filters: new ReportAccessControlFilters(
                        ElevatorActivity: new ElevatorActivityReportFilters(ElevatorIds: [Guid.NewGuid()])))));

        Assert.Contains("Filter group", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccessRuleConfiguration_UsesTypedConfigurationFilters()
    {
        var handler = new CapturingHandler();
        var ownerId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");

        await ReportAccessControlTool.Execute(TestHelpers.FakeManager(handler),
            new ReportAccessControlRequest(
                AccessControlReportType.AccessRuleConfiguration,
                Filters: new ReportAccessControlFilters(
                    AccessRuleConfiguration: new AccessRuleConfigurationReportFilters(
                        AccessRuleType: AccessRuleTypeFilter.Permanent,
                        SortOrder: ReportSortOrder.Descending,
                        CustomFields: [new ReportCustomFieldFilter(Guid.Parse("dddddddd-eeee-ffff-0000-111111111111"), NumericValue: 7)],
                        Name: "Main Entrance",
                        NameSearchMode: ReportStringSearchMode.Contains,
                        Description: "Staff access",
                        DescriptionSearchMode: ReportStringSearchMode.StartsWith,
                        LogicalId: 18,
                        Owner: ownerId,
                        StrictResults: false,
                        GuidsOnly: true,
                        DownloadAllRelatedData: true))));

        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains(
            "report/AccessRuleConfiguration?q=SortOrder=Descending,AccessRuleType=Permanent,Name=Main Entrance,NameSearchMode=Contains," +
            "Description=Staff access,DescriptionSearchMode=StartsWith,LogicalId=18," +
            $"Owner={ownerId},StrictResults=False,GuidsOnly=True,DownloadAllRelatedData=True," +
            "CustomFields@CustomField(dddddddd-eeee-ffff-0000-111111111111,7),PageSize=50,Page=1",
            query,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccessControlReportType.TimeAttendance)]
    [InlineData(AccessControlReportType.AccessRuleConfiguration)]
    public async Task UnsupportedMaximumResultCount_IsRejected(AccessControlReportType reportType)
    {
        var request = reportType == AccessControlReportType.TimeAttendance
            ? new ReportAccessControlRequest(reportType, TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z", MaximumResultCount: 25)
            : new ReportAccessControlRequest(reportType, MaximumResultCount: 25);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(), request));
    }

    [Fact]
    public async Task TopLevelFiltersForDifferentReport_AreRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(
                    AccessControlReportType.TimeAttendance,
                    Id: EntityId,
                    TimeStart: "2024-01-01T00:00:00Z",
                    TimeEnd: "2024-01-02T00:00:00Z")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(
                    AccessControlReportType.AccessRuleConfiguration,
                    TimeStart: "2024-01-01T00:00:00Z",
                    TimeEnd: "2024-01-02T00:00:00Z")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(
                    AccessControlReportType.ZoneActivity,
                    EntityId,
                    "2024-01-01T00:00:00Z",
                    "2024-01-02T00:00:00Z",
                    PageSize: 100)));
    }

    [Fact]
    public void RequestSchema_UsesTypedNestedFiltersAndBoundedLimits()
    {
        Assert.Equal(typeof(ReportAccessControlFilters), typeof(ReportAccessControlRequest).GetProperty(nameof(ReportAccessControlRequest.Filters))!.PropertyType);
        Assert.Equal(typeof(ZoneActivityReportFilters), typeof(ReportAccessControlFilters).GetProperty(nameof(ReportAccessControlFilters.ZoneActivity))!.PropertyType);
        Assert.Equal(typeof(List<Guid>), typeof(ZoneActivityReportFilters).GetProperty(nameof(ZoneActivityReportFilters.ZoneIds))!.PropertyType);
        Assert.Equal(typeof(List<Genetec.Mcp.SecurityCenter.ScEventType>), typeof(AccessControlActivityReportFilters).GetProperty(nameof(AccessControlActivityReportFilters.Events))!.PropertyType);

        var invalidResultCount = new ReportAccessControlRequest(AccessControlReportType.UnusedCredentials, MaximumResultCount: 100001);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalidResultCount, new ValidationContext(invalidResultCount), errors, validateAllProperties: true));

        var invalidPageSize = new ReportAccessControlRequest(AccessControlReportType.AccessRuleConfiguration, PageSize: 101);
        Assert.False(Validator.TryValidateObject(invalidPageSize, new ValidationContext(invalidPageSize), [], validateAllProperties: true));

        var invalidCustomField = new ReportCustomFieldFilter(Guid.NewGuid(), TextValue: "text", NumericValue: 1);
        Assert.False(Validator.TryValidateObject(invalidCustomField, new ValidationContext(invalidCustomField), [], validateAllProperties: true));
    }

    [Theory]
    [InlineData(AccessControlReportType.UnusedCredentials)]
    [InlineData(AccessControlReportType.UnusedCardholders)]
    public async Task UnusedReport_WithOnlyOneTimeBoundary_ThrowsArgumentException(
        AccessControlReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(reportType,
                    TimeStart: "2020-01-01T00:00:00Z")));

        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessControlReportType.UnusedCredentials)]
    [InlineData(AccessControlReportType.UnusedCardholders)]
    public async Task UnusedReport_WithoutTimeRange_ThrowsArgumentException(
        AccessControlReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(reportType)));

        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(AccessControlReportType.ZoneActivity)]
    [InlineData(AccessControlReportType.ElevatorActivity)]
    [InlineData(AccessControlReportType.AcuActivity)]
    [InlineData(AccessControlReportType.IntrusionActivity)]
    public async Task EntityTimeReport_MissingId_ThrowsArgumentException(AccessControlReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(reportType,
                    TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z")));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessControlReportType.ZoneActivity)]
    [InlineData(AccessControlReportType.ElevatorActivity)]
    [InlineData(AccessControlReportType.AcuActivity)]
    [InlineData(AccessControlReportType.TimeAttendance)]
    [InlineData(AccessControlReportType.IntrusionActivity)]
    public async Task TimeReport_MissingTimeStart_ThrowsArgumentException(AccessControlReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(reportType,
                    Id: reportType == AccessControlReportType.TimeAttendance ? null : Guid.NewGuid(),
                    TimeEnd: "2024-01-02T00:00:00Z")));
        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessControlReportType.ZoneActivity)]
    [InlineData(AccessControlReportType.ElevatorActivity)]
    [InlineData(AccessControlReportType.AcuActivity)]
    [InlineData(AccessControlReportType.TimeAttendance)]
    [InlineData(AccessControlReportType.IntrusionActivity)]
    public async Task TimeReport_MissingTimeEnd_ThrowsArgumentException(AccessControlReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest(reportType,
                    Id: reportType == AccessControlReportType.TimeAttendance ? null : Guid.NewGuid(),
                    TimeStart: "2024-01-01T00:00:00Z")));
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownReportType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportAccessControlTool.Execute(TestHelpers.FakeManager(),
                new ReportAccessControlRequest((AccessControlReportType)999)));

        Assert.Contains("Unknown report type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }
}
