using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialSearchToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Search26Bit_NullFacilityCode_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialSearchTool.Execute(TestHelpers.FakeManager(),
                new CredentialSearchRequest(CredentialSearchAction.Search26Bit, CardNumber: 100)));

        Assert.Contains("facility_code", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search26Bit_NullCardNumber_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialSearchTool.Execute(TestHelpers.FakeManager(),
                new CredentialSearchRequest(CredentialSearchAction.Search26Bit, FacilityCode: 1)));

        Assert.Contains("card_number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search26Bit_UsesExpectedWebSdkQuery()
    {
        var handler = new CapturingHandler();

        await CredentialSearchTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.Search26Bit,
                FacilityCode: 123, CardNumber: 456, PageSize: 10));

        AssertQueryContains(handler,
            "report/CredentialConfiguration?q=UniqueIds@Genetec.Sdk.Credentials.WiegandStandardCredentialFormat(123,456),PageSize=10");
    }

    [Fact]
    public async Task SearchHidH10306_UsesExpectedWebSdkQuery()
    {
        var handler = new CapturingHandler();

        await CredentialSearchTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.SearchHidH10306,
                FacilityCode: 123, CardNumber: 456, PageSize: 10));

        AssertQueryContains(handler,
            "report/CredentialConfiguration?q=UniqueIds@Genetec.Sdk.Credentials.WiegandH10306CredentialFormat(123,456),PageSize=10");
    }

    [Fact]
    public async Task SearchCsn_NullCsn_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialSearchTool.Execute(TestHelpers.FakeManager(),
                new CredentialSearchRequest(CredentialSearchAction.SearchCsn)));

        Assert.Contains("csn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchCsn_UsesDecimalCsnInWebSdkQuery()
    {
        var handler = new CapturingHandler();

        await CredentialSearchTool.Execute(
            TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.SearchCsn,
                Csn: 11259375, PageSize: 10));

        AssertQueryContains(handler,
            "report/CredentialConfiguration?q=UniqueIds@Genetec.Sdk.Credentials.WiegandCsn32CredentialFormat(11259375),PageSize=10");
    }

    [Fact]
    public async Task SearchLicensePlate_NullPlateNumber_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialSearchTool.Execute(TestHelpers.FakeManager(),
                new CredentialSearchRequest(CredentialSearchAction.SearchLicensePlate)));

        Assert.Contains("plate_number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchLicensePlate_UsesExpectedWebSdkQuery()
    {
        var handler = new CapturingHandler();

        await CredentialSearchTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.SearchLicensePlate,
                PlateNumber: "ON ABC 123", PageSize: 10));

        AssertQueryContains(handler,
            "report/CredentialConfiguration?q=UniqueIds@Genetec.Sdk.Credentials.LicensePlateCredentialFormat(ON ABC 123),PageSize=10");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchCustom_UsesExpectedWebSdkQuery(bool withValues)
    {
        var handler = new CapturingHandler();
        var formatId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await CredentialSearchTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.SearchCustom,
                CustomFormatId: formatId,
                CustomValues: withValues ? new Dictionary<string, string> { ["FacilityCode"] = "123" } : null,
                PageSize: 10,
                Page: 2));

        var filter = withValues
            ? $"UniqueIds@CustomCredentialFormat({formatId},{{{{FacilityCode,123}}}})"
            : $"FormatType@{formatId}";
        AssertQueryContains(handler, $"report/CredentialConfiguration?q={filter},PageSize=10,Page=2");
    }

    [Fact]
    public async Task FindUnassigned_UsesExpectedWebSdkQuery()
    {
        var handler = new CapturingHandler();

        await CredentialSearchTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialSearchRequest(CredentialSearchAction.FindUnassigned, PageSize: 10));

        AssertQueryContains(handler,
            "report/CredentialConfiguration?q=UnassignedOnly=True,PageSize=10");
    }

    [Fact]
    public async Task SearchCustom_NullFormatId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialSearchTool.Execute(TestHelpers.FakeManager(),
                new CredentialSearchRequest(CredentialSearchAction.SearchCustom)));

        Assert.Contains("custom_format_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertQueryContains(CapturingHandler handler, string expected)
    {
        var request = Assert.Single(handler.Requests);
        var query = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Assert.Contains(expected, query, StringComparison.Ordinal);
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
