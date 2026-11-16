using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class ReportAccessControlToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("%2CGuidsOnly%3DTrue")]
    [InlineData("#literal")]
    [InlineData("+literal")]
    [InlineData(@", (literal) @site\item")]
    public async Task AccessRuleConfiguration_LiteralName_ReturnsOnlyMatchingRule(string suffix)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_AccessRuleReport_{Guid.NewGuid():N}{suffix}";
        var rule = Assert.IsType<EntityCreateResult>(await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem(name)])));
        sc.TrackForCleanup(rule.Guid);
        var request = new ReportAccessControlRequest(AccessControlReportType.AccessRuleConfiguration,
            Filters: new ReportAccessControlFilters(AccessRuleConfiguration:
                new AccessRuleConfigurationReportFilters(Name: name, NameSearchMode: ReportStringSearchMode.Is)));

        var matching = Assert.IsType<string>(await ReportAccessControlTool.Execute(sc.Manager, request));
        Assert.Contains(rule.Guid, IntegrationTestHelpers.ExtractGuids(matching));
        var missing = Assert.IsType<string>(await ReportAccessControlTool.Execute(sc.Manager,
            request with { Filters = new ReportAccessControlFilters(AccessRuleConfiguration:
                new AccessRuleConfigurationReportFilters(Name: name + "&missing", NameSearchMode: ReportStringSearchMode.Is)) }));
        Assert.DoesNotContain(rule.Guid, IntegrationTestHelpers.ExtractGuids(missing));
    }

    [Fact]
    public async Task AccessRuleConfiguration_WithTypedPageControls_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportAccessControlTool.Execute(sc.Manager,
            new ReportAccessControlRequest(
                AccessControlReportType.AccessRuleConfiguration,
                Page: 1,
                PageSize: 50,
                Filters: new ReportAccessControlFilters(
                    AccessRuleConfiguration: new AccessRuleConfigurationReportFilters(
                        SortOrder: ReportSortOrder.Descending,
                        AccessRuleType: AccessRuleTypeFilter.Permanent))));

        AssertReportResult(result);
    }

    [Theory]
    [InlineData(AccessControlReportType.UnusedCredentials, 1)]
    [InlineData(AccessControlReportType.UnusedCredentials, 10)]
    [InlineData(AccessControlReportType.UnusedCardholders, 1)]
    [InlineData(AccessControlReportType.UnusedCardholders, 10)]
    public async Task UnusedEntities_ReturnExistingEntitiesThatCanBeReadBack(
        AccessControlReportType reportType, int maximumResultCount)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var result = await ReportAccessControlTool.Execute(sc.Manager,
            new ReportAccessControlRequest(reportType,
                TimeStart: DateTime.UtcNow.AddDays(-1).ToString("O"),
                TimeEnd: DateTime.UtcNow.ToString("O"),
                MaximumResultCount: maximumResultCount));

        var json = Assert.IsType<string>(result);
        using var document = IntegrationTestHelpers.ParseJson(json);
        var rows = IntegrationTestHelpers.GetRows(document.RootElement);
        var ids = IntegrationTestHelpers.ExtractGuids(json);
        var status = document.RootElement.TryGetProperty("Rsp", out var rsp)
            ? rsp.GetProperty("Status").GetString() : "Ok";
        output.WriteLine($"{reportType}: returned_rows={rows.GetArrayLength()}, requested_limit={maximumResultCount}, status={status}.");
        Assert.InRange(rows.GetArrayLength(), 1, maximumResultCount);
        Assert.NotEmpty(ids);
        Assert.Equal(rows.GetArrayLength(), ids.Count);
        foreach (var id in ids)
        {
            using var entity = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(id.ToString()));
            var entityResult = IntegrationTestHelpers.GetResult(entity.RootElement);
            Assert.Equal(id, entityResult.GetProperty("Guid").GetGuid());
            var entityType = entityResult.GetProperty("EntityType").GetString();
            if (reportType == AccessControlReportType.UnusedCredentials)
                Assert.Equal("Credential", entityType);
            else
                Assert.Contains(entityType, new[] { "Cardholder", "Visitor" });
        }
    }

    [Theory]
    [InlineData(AccessControlReportType.ZoneActivity, "Zone")]
    [InlineData(AccessControlReportType.ElevatorActivity, "Elevator")]
    [InlineData(AccessControlReportType.AcuActivity, "Unit")]
    public async Task EntityActivity_WithTimeRange_ReturnsResult(
        AccessControlReportType reportType,
        string entityType)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var entityId = await GetFirstEntityIdAsync(entityType);
        var activityFilters = new AccessControlActivityReportFilters(SortOrder: ReportSortOrder.Descending);
        var filters = reportType switch
        {
            AccessControlReportType.ZoneActivity => new ReportAccessControlFilters(
                ZoneActivity: new ZoneActivityReportFilters(Activity: activityFilters)),
            AccessControlReportType.ElevatorActivity => new ReportAccessControlFilters(
                ElevatorActivity: new ElevatorActivityReportFilters(Activity: activityFilters)),
            AccessControlReportType.AcuActivity => new ReportAccessControlFilters(
                AcuActivity: new AcuActivityReportFilters(Activity: activityFilters)),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType))
        };

        var result = await ReportAccessControlTool.Execute(sc.Manager,
            new ReportAccessControlRequest(
                reportType,
                Id: entityId,
                TimeStart: "2024-01-01T00:00:00Z",
                TimeEnd: "2024-01-02T00:00:00Z",
                Filters: filters));

        AssertReportResult(result);
    }

    [Fact]
    public async Task TimeAttendance_WithTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ReportAccessControlTool.Execute(sc.Manager,
            new ReportAccessControlRequest(
                AccessControlReportType.TimeAttendance,
                TimeStart: "2024-01-01T00:00:00Z",
                TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportAccessControlFilters(
                    TimeAttendance: new TimeAttendanceReportFilters(SortOrder: ReportSortOrder.Ascending))));

        AssertReportResult(result);
    }

    [Fact]
    public async Task IntrusionActivity_WithAreaAndTimeRange_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var intrusionAreaId = await GetFirstEntityIdAsync("IntrusionArea");

        var result = await ReportAccessControlTool.Execute(sc.Manager,
            new ReportAccessControlRequest(
                AccessControlReportType.IntrusionActivity,
                Id: intrusionAreaId,
                TimeStart: "2024-01-01T00:00:00Z",
                TimeEnd: "2024-01-02T00:00:00Z",
                Filters: new ReportAccessControlFilters(
                    IntrusionActivity: new IntrusionActivityReportFilters(
                        Activity: new AccessControlActivityReportFilters(SortOrder: ReportSortOrder.Descending)))));

        AssertReportResult(result);
    }

    private async Task<Guid> GetFirstEntityIdAsync(string entityType)
    {
        var json = await sc.Manager.GetClient().ListEntities(entityType);
        var id = IntegrationTestHelpers.ExtractFirstGuid(json);
        Assert.True(id.HasValue, $"No {entityType} entities are configured for integration tests.");
        return id.Value;
    }

    private static void AssertReportResult(object result)
    {
        var json = Assert.IsType<string>(result);
        Assert.NotEmpty(json);
    }

}
