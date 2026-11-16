using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CredentialSearchToolTests(ScIntegrationFixture sc, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CredentialSearchAction.Search26Bit, 1)]
    [InlineData(CredentialSearchAction.Search26Bit, 2)]
    [InlineData(CredentialSearchAction.Search26Bit, 100)]
    [InlineData(CredentialSearchAction.SearchHidH10306, 1)]
    [InlineData(CredentialSearchAction.SearchHidH10306, 2)]
    [InlineData(CredentialSearchAction.SearchHidH10306, 100)]
    [InlineData(CredentialSearchAction.SearchCsn, 1)]
    [InlineData(CredentialSearchAction.SearchCsn, 2)]
    [InlineData(CredentialSearchAction.SearchCsn, 100)]
    [InlineData(CredentialSearchAction.SearchLicensePlate, 1)]
    [InlineData(CredentialSearchAction.SearchLicensePlate, 2)]
    [InlineData(CredentialSearchAction.SearchLicensePlate, 100)]
    public Task SmallPages_FormatSearch_RespectLimitAndIdentity(CredentialSearchAction action, int pageSize)
        => IntegrationTestHelpers.AssertSmallPages(sc,
            (size, page) => CredentialSearchTool.Execute(sc.Manager,
                new CredentialSearchRequest(action, FacilityCode: 1, CardNumber: 1,
                    Csn: 1, PlateNumber: "TESTPLATE", PageSize: size, Page: page)), pageSize, output.WriteLine);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public async Task SmallPages_CustomSearch_RespectLimitAndIdentity(int pageSize)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var existing = await FindExistingCustomCredentialAsync(sc);
        Assert.NotNull(existing);
        await IntegrationTestHelpers.AssertSmallPages(sc,
            (size, page) => CredentialSearchTool.Execute(sc.Manager,
                new CredentialSearchRequest(CredentialSearchAction.SearchCustom,
                    CustomFormatId: existing.FormatId, CustomValues: existing.Values,
                    PageSize: size, Page: page)), pageSize, output.WriteLine);
    }

    // â”€â”€ Integration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Search26Bit_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.Search26Bit, FacilityCode: 1, CardNumber: 1));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchHidH10306_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.SearchHidH10306, FacilityCode: 1, CardNumber: 1));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchCsn_CreatedCsn32Credential_ReturnsMatchingCredential()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var csn = Random.Shared.Next(1, int.MaxValue);
        var created = Assert.IsType<EntityCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_CsnSearch_{Guid.NewGuid():N}",
                CredentialFormatType.Csn32Bit, Csn: csn)])));
        sc.TrackForCleanup(created.Guid);

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.SearchCsn, Csn: csn));

        Assert.Contains(created.Guid, IntegrationTestHelpers.ExtractGuids(Assert.IsType<string>(result)));
    }

    [Fact]
    public async Task SearchLicensePlate_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.SearchLicensePlate, PlateNumber: "TESTPLATE"));

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("%2C")]
    [InlineData("&x")]
    [InlineData("#x")]
    [InlineData("+x")]
    public async Task SearchLicensePlate_LiteralSuffix_ExcludesKnownCredential(string suffix)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var plate = $"M{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var credential = Assert.IsType<EntityCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_PlateSearch_{Guid.NewGuid():N}",
                CredentialFormatType.LicensePlate, PlateNumber: plate, PlateState: "ON")])));
        sc.TrackForCleanup(credential.Guid);
        var request = new CredentialSearchRequest(CredentialSearchAction.SearchLicensePlate, PlateNumber: plate);

        var matching = Assert.IsType<string>(await CredentialSearchTool.Execute(sc.Manager, request));
        Assert.Contains(credential.Guid, IntegrationTestHelpers.ExtractGuids(matching));
        var missing = Assert.IsType<string>(await CredentialSearchTool.Execute(sc.Manager,
            request with { PlateNumber = plate + suffix }));
        Assert.DoesNotContain(credential.Guid, IntegrationTestHelpers.ExtractGuids(missing));
    }

    [Fact]
    public async Task SearchCustom_ExistingCustomFormat_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var existing = await FindExistingCustomCredentialAsync(sc);
        Assert.True(existing is not null,
            "No custom-format credential with a readable format ID and field values was found in the first 300 credentials in Security Center.");

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.SearchCustom,
                CustomFormatId: existing!.FormatId,
                CustomValues: existing.Values));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindUnassigned_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await CredentialSearchTool.Execute(sc.Manager,
            new CredentialSearchRequest(CredentialSearchAction.FindUnassigned));

        Assert.NotNull(result);
    }

    internal static async Task<ExistingCustomCredential?> FindExistingCustomCredentialAsync(ScIntegrationFixture sc)
    {
        var client = sc.Manager.GetClient();
        foreach (var id in await FindCredentialIdsAsync(client))
        {
            using var document = JsonDocument.Parse(await client.GetEntityTrimmed(id.ToString()));
            if (!document.RootElement.TryGetProperty("Format", out var format) ||
                format.ValueKind != JsonValueKind.Object ||
                !format.TryGetProperty("IsCustomFormat", out var isCustom) ||
                isCustom.ValueKind != JsonValueKind.True ||
                !Guid.TryParse(ReadProperty(format, "FormatId"), out var formatId))
                continue;

            if (format.TryGetProperty("Values", out var fields) && fields.ValueKind == JsonValueKind.Object)
                return new ExistingCustomCredential(formatId, ReadCustomValues(format));
        }

        return null;
    }

    private static async Task<IReadOnlyList<Guid>> FindCredentialIdsAsync(SecurityCenterRestClient client)
    {
        const int pageSize = 100;
        const int maxPages = 3;
        var ids = new List<Guid>();
        for (var page = 1; page <= maxPages; page++)
        {
            var credentialList = await client.ListEntities("Credential", pageSize, page, ["Name"]);
            var pageIds = IntegrationTestHelpers.ExtractGuids(credentialList);
            ids.AddRange(pageIds);
            if (pageIds.Count < pageSize)
                return ids;
        }

        return ids;
    }

    private static Dictionary<string, string> ReadCustomValues(JsonElement format)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!format.TryGetProperty("Values", out var fields) || fields.ValueKind != JsonValueKind.Object)
            return values;

        foreach (var field in fields.EnumerateObject())
        {
            if (field.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                values[field.Name] = field.Value.ValueKind == JsonValueKind.String
                    ? field.Value.GetString()!
                    : field.Value.ToString();
        }

        return values;
    }

    private static string? ReadProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    internal sealed record ExistingCustomCredential(Guid FormatId, Dictionary<string, string> Values);
}
