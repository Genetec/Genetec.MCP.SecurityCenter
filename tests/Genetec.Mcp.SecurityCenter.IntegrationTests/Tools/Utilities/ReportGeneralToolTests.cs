using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class ReportGeneralToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task AuditTrails_WithTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.AuditTrails,
                TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportGeneralFilters(
                    SortOrder: ReportSortOrder.Descending,
                    AuditTrails: new AuditTrailsReportFilters(
                        Applications: [GeneralApplicationType.SecurityDesk]))));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task Incident_WithTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.Incident,
                TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportGeneralFilters(
                    Incident: new IncidentReportFilters(
                        NotesSearchMode: ReportStringSearchMode.Contains,
                        ReferenceGuids: [Guid.Empty]))));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task HealthStatistics_WithTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.HealthStatistics,
                TimeStart: "2024-01-01T00:00:00Z", TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportGeneralFilters(
                    HealthStatistics: new HealthStatisticsReportFilters(
                        Sources: [Guid.Empty]))));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(50)]
    public async Task HealthStatistics_SourceThreshold_PreservesNativeRowsAndStatus(int? sourceCount)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        const string start = "2024-01-01T00:00:00Z";
        const string end = "2024-01-02T00:00:00Z";
        var expectedJson = await sc.Manager.GetClient().RunReport("HealthStatistics",
            $"TimeRange.SetTimeRange({start},{end}),MaximumResultCount={sourceCount ?? 50}");
        var actualJson = Assert.IsType<string>(await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.HealthStatistics,
                TimeStart: start, TimeEnd: end, MaximumSourceCount: sourceCount)));
        using var expected = IntegrationTestHelpers.ParseJson(expectedJson);
        using var actual = IntegrationTestHelpers.ParseJson(actualJson);
        var expectedRows = IntegrationTestHelpers.GetRows(expected.RootElement);
        var actualRows = IntegrationTestHelpers.GetRows(actual.RootElement);
        Assert.True(expectedRows.GetArrayLength() > (sourceCount ?? 50),
            "The configured instance must expose multiple health statistic rows to verify that source selection is not treated as a row cap.");
        Assert.Equal(expectedRows.EnumerateArray().Select(row => row.GetRawText()), actualRows.EnumerateArray().Select(row => row.GetRawText()));
        if (expected.RootElement.TryGetProperty("Rsp", out var expectedRsp))
        {
            var actualRsp = actual.RootElement.GetProperty("Rsp");
            Assert.Equal(expectedRsp.GetProperty("Status").GetString(), actualRsp.GetProperty("Status").GetString());
            Assert.Equal(expectedRsp.EnumerateObject().Select(property => property.Name), actualRsp.EnumerateObject().Select(property => property.Name));
        }
    }

    [Fact]
    public async Task UserGroup_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.UserGroup,
                Filters: new ReportGeneralFilters(
                    UserGroup: new UserGroupReportFilters(
                        Status: [GeneralUserStatus.Activated],
                        EntitySharingCriteria: new EntitySharingCriteriaFilter(Shared: true),
                        PageSize: 10,
                        DownloadAllRelatedData: false))));

        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Theory]
    [InlineData("%2CGuidsOnly%3DTrue")]
    [InlineData("#literal")]
    [InlineData("+literal")]
    [InlineData(@", (literal) @site\item")]
    public async Task UserGroup_NameFilter_IncludesMatchingGroupAndExcludesDifferentName(string suffix)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var uniqueGroupName = $"MCP_ReportGeneral_Group_{Guid.NewGuid():N}{suffix}";
        var group = Assert.IsType<EntityCreateResult>(await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem(uniqueGroupName)])));
        sc.TrackForCleanup(group.Guid);

        var matchingResult = (string)await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.UserGroup,
                Filters: new ReportGeneralFilters(
                    UserGroup: new UserGroupReportFilters(
                        Name: uniqueGroupName,
                        NameSearchMode: ReportStringSearchMode.Is))));

        var nonMatchingResult = (string)await ReportGeneralTool.Execute(sc.Manager,
            new ReportGeneralRequest(GeneralReportType.UserGroup,
                Filters: new ReportGeneralFilters(
                    UserGroup: new UserGroupReportFilters(
                        Name: $"{uniqueGroupName}&missing",
                        NameSearchMode: ReportStringSearchMode.Is))));

        Assert.Contains(group.Guid, IntegrationTestHelpers.ExtractGuids(matchingResult));
        Assert.Empty(IntegrationTestHelpers.ExtractGuids(nonMatchingResult));
    }
}
