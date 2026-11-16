using System.ComponentModel.DataAnnotations;
using System.Net;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class ReportLprToolTests
{
    private const string TimeStart = "2024-01-01T00:00:00Z";
    private const string TimeEnd = "2024-01-02T00:00:00Z";
    private static readonly Guid EntityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }

    [Theory]
    [InlineData(LprReportType.LprHit)]
    [InlineData(LprReportType.PatrollerPositions)]
    [InlineData(LprReportType.LpmDailyUsage)]
    [InlineData(LprReportType.ParkingSession)]
    public async Task TimeReport_MissingTimeStart_ThrowsArgumentException(LprReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(), ValidRequest(reportType, includeStart: false)));
        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(LprReportType.LprHit)]
    [InlineData(LprReportType.PatrollerPositions)]
    [InlineData(LprReportType.LpmDailyUsage)]
    [InlineData(LprReportType.ParkingSession)]
    public async Task TimeReport_MissingTimeEnd_ThrowsArgumentException(LprReportType reportType)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(), ValidRequest(reportType, includeEnd: false)));
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LpmDailyUsage_MissingPatrollerId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(),
                new ReportLprRequest(LprReportType.LpmDailyUsage, TimeStart: TimeStart, TimeEnd: TimeEnd)));
        Assert.Contains("patroller_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(LprReportType.LprHit)]
    [InlineData(LprReportType.PatrollerPositions)]
    [InlineData(LprReportType.ParkingSession)]
    [InlineData(LprReportType.ParkingInventory)]
    public async Task EntityReport_MissingTargetCollection_ThrowsArgumentException(LprReportType reportType)
    {
        var request = reportType switch
        {
            LprReportType.LprHit => new ReportLprRequest(reportType, TimeStart: TimeStart, TimeEnd: TimeEnd),
            LprReportType.PatrollerPositions => new ReportLprRequest(reportType, TimeStart: TimeStart, TimeEnd: TimeEnd),
            LprReportType.ParkingSession => new ReportLprRequest(reportType, TimeStart: TimeStart, TimeEnd: TimeEnd),
            _ => new ReportLprRequest(reportType)
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => ReportLprTool.Execute(TestHelpers.FakeManager(), request));
        var parameter = reportType switch
        {
            LprReportType.LprHit => "filters.lpr_hit.source_ids",
            LprReportType.PatrollerPositions => "patrollers",
            _ => "parking_zones"
        };
        Assert.Contains(parameter, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LprHit_MissingOrEmptySourceIds_ThrowsArgumentException(bool empty)
    {
        var filters = new ReportLprHitFilters(empty ? [] : null!);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(),
                new ReportLprRequest(LprReportType.LprHit, TimeStart: TimeStart, TimeEnd: TimeEnd,
                    Filters: new ReportLprFilters(LprHit: filters))));

        Assert.Contains("filters.lpr_hit.source_ids", ex.Message, StringComparison.OrdinalIgnoreCase);
        TestHelpers.AssertRequiredProperty<ReportLprHitFilters>(nameof(ReportLprHitFilters.SourceIds));
        Assert.False(Validator.TryValidateProperty(filters.SourceIds,
            new ValidationContext(filters) { MemberName = nameof(ReportLprHitFilters.SourceIds) }, []));
    }

    [Fact]
    public async Task MismatchedFilterObject_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(),
                new ReportLprRequest(LprReportType.LpmDailyUsage,
                    TimeStart: TimeStart, TimeEnd: TimeEnd, PatrollerId: EntityId,
                    Filters: new ReportLprFilters(ParkingSession: new ReportLprParkingSessionFilters([EntityId])))));
        Assert.Contains("parking_session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PatrollerId_OnNonDailyUsageReport_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(),
                new ReportLprRequest(LprReportType.LprHit, TimeStart: TimeStart, TimeEnd: TimeEnd, PatrollerId: EntityId)));
        Assert.Contains("patroller_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownReportType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(), new ReportLprRequest((LprReportType)999)));
        Assert.Contains("Unknown report type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public void MaximumResultCount_IsBounded(int maximumResultCount)
    {
        var request = new ReportLprRequest(LprReportType.LprHit, MaximumResultCount: maximumResultCount);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ReportLprRequest.MaximumResultCount)));
    }

    [Fact]
    public void Filters_ExposeTypedNestedSchemaAndRejectEmptyCollections()
    {
        Assert.Equal(typeof(ReportLprFilters), typeof(ReportLprRequest).GetProperty(nameof(ReportLprRequest.Filters))!.PropertyType);
        Assert.Equal(typeof(List<LprHitType>), typeof(ReportLprHitFilters).GetProperty(nameof(ReportLprHitFilters.HitTypes))!.PropertyType);
        var hitTypes = typeof(ReportLprHitFilters).GetProperty(nameof(ReportLprHitFilters.HitTypes))!;
        Assert.NotNull(hitTypes.GetCustomAttributes(typeof(MinLengthAttribute), inherit: true).Cast<MinLengthAttribute>().Single());
        Assert.False(Validator.TryValidateProperty(
            new List<LprHitType>(),
            new ValidationContext(new ReportLprHitFilters([EntityId])) { MemberName = nameof(ReportLprHitFilters.HitTypes) },
            []));
    }

    [Fact]
    public async Task LpmDailyUsage_InvalidTimeZoneId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportLprTool.Execute(TestHelpers.FakeManager(),
                new ReportLprRequest(LprReportType.LpmDailyUsage,
                    TimeStart: TimeStart, TimeEnd: TimeEnd, PatrollerId: EntityId,
                    Filters: new ReportLprFilters(LpmDailyUsage: new ReportLprDailyUsageFilters(RefTimeZoneId: "not-a-time-zone")))));
        Assert.Contains("ref_time_zone_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(LprReportType.LprHit, "LprHit", "TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=50,SourceIds@11111111-2222-3333-4444-555555555555")]
    [InlineData(LprReportType.PatrollerPositions, "PatrollerPositions", "Patrollers@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=50")]
    [InlineData(LprReportType.LpmDailyUsage, "LpmDailyUsage", "PatrollerId=11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=50")]
    [InlineData(LprReportType.ParkingSession, "ParkingSession", "ParkingZones@11111111-2222-3333-4444-555555555555,TimeRange.SetTimeRange(2024-01-01T00:00:00Z,2024-01-02T00:00:00Z),MaximumResultCount=50")]
    [InlineData(LprReportType.ParkingInventory, "ParkingZoneOccupancy", "ParkingZones@11111111-2222-3333-4444-555555555555,MaximumResultCount=50")]
    public async Task ReportType_UsesExpectedWebSdkRouteAndQuery(LprReportType reportType, string sdkReportType, string expectedQuery)
    {
        var handler = new CapturingHandler();
        var result = await ReportLprTool.Execute(TestHelpers.FakeManager(handler), ValidRequest(reportType));

        Assert.Equal("[]", result);
        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal($"/WebSdk/report/{sdkReportType}?q={expectedQuery}", query);
    }

    [Fact]
    public async Task LprHit_UsesTypedListsEnumsAndCustomFieldValues()
    {
        var handler = new CapturingHandler();
        await ReportLprTool.Execute(TestHelpers.FakeManager(handler),
            new ReportLprRequest(LprReportType.LprHit, TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(
                    CustomFields: [new ReportCustomFieldFilter(EntityId, BooleanValue: true)],
                    LprHit: new ReportLprHitFilters(
                        SourceIds: [EntityId],
                        HitTypes: [LprHitType.Hotlist, LprHitType.Permit],
                        Plates: ["A123", "B456"],
                        CustomFieldFilters: [new ReportLprCustomFieldFilter(EntityId, "parked")]))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Contains("HitTypes@Hotlist@Permit", query, StringComparison.Ordinal);
        Assert.Contains("Plates@A123@B456", query, StringComparison.Ordinal);
        Assert.Contains("CustomFieldFilters={{" + EntityId.ToString("D") + ",parked}}", query, StringComparison.Ordinal);
        Assert.Contains($"CustomFields@CustomField({EntityId:D},True)", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LpmDailyUsage_RoutesPatrollerIdToSdkProperty()
    {
        var handler = new CapturingHandler();
        await ReportLprTool.Execute(TestHelpers.FakeManager(handler),
            new ReportLprRequest(LprReportType.LpmDailyUsage,
                TimeStart: TimeStart, TimeEnd: TimeEnd, PatrollerId: EntityId,
                Filters: new ReportLprFilters(LpmDailyUsage: new ReportLprDailyUsageFilters(RelativeTimeRange: true))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Contains($"PatrollerId={EntityId:D}", query, StringComparison.Ordinal);
        Assert.Contains("RelativeTimeRange=True", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParkingFilters_UseSdkPropertyNamesAndInvariantDurations()
    {
        var handler = new CapturingHandler();
        await ReportLprTool.Execute(TestHelpers.FakeManager(handler),
            new ReportLprRequest(LprReportType.ParkingSession,
                TimeStart: TimeStart, TimeEnd: TimeEnd,
                Filters: new ReportLprFilters(ParkingSession: new ReportLprParkingSessionFilters(
                    [EntityId],
                    MinimumDurationOfStay: TimeSpan.FromMinutes(5),
                    MaximumDurationOfStay: TimeSpan.FromMinutes(30),
                    PermitIds: [EntityId],
                    SessionStates: [LprSessionState.Valid]))));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Contains("MinimumDurationOfStay=00:05:00", query, StringComparison.Ordinal);
        Assert.Contains("MaximumDurationOfStay=00:30:00", query, StringComparison.Ordinal);
        Assert.Contains($"PermitIds@{EntityId:D}", query, StringComparison.Ordinal);
        Assert.Contains("SessionStates@Valid", query, StringComparison.Ordinal);
    }

    private static ReportLprRequest ValidRequest(LprReportType reportType, bool includeStart = true, bool includeEnd = true)
    {
        var start = includeStart ? TimeStart : null;
        var end = includeEnd ? TimeEnd : null;
        return reportType switch
        {
            LprReportType.LprHit => new ReportLprRequest(reportType, TimeStart: start, TimeEnd: end,
                Filters: new ReportLprFilters(LprHit: new ReportLprHitFilters([EntityId]))),
            LprReportType.PatrollerPositions => new ReportLprRequest(reportType, TimeStart: start, TimeEnd: end,
                Filters: new ReportLprFilters(PatrollerPositions: new ReportLprPatrollerPositionFilters([EntityId]))),
            LprReportType.LpmDailyUsage => new ReportLprRequest(reportType, TimeStart: start, TimeEnd: end, PatrollerId: EntityId),
            LprReportType.ParkingSession => new ReportLprRequest(reportType, TimeStart: start, TimeEnd: end,
                Filters: new ReportLprFilters(ParkingSession: new ReportLprParkingSessionFilters([EntityId]))),
            LprReportType.ParkingInventory => new ReportLprRequest(reportType,
                Filters: new ReportLprFilters(ParkingInventory: new ReportLprParkingInventoryFilters([EntityId]))),
            _ => new ReportLprRequest(reportType, TimeStart: start, TimeEnd: end)
        };
    }
}
