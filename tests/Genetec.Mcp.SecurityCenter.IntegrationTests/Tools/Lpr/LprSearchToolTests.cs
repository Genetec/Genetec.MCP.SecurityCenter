using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;
using static Genetec.Mcp.SecurityCenter.Client.InputValidation;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class LprSearchToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("%2CMaximumResultCount%3D1")]
    [InlineData("&literal")]
    [InlineData("#literal")]
    [InlineData("+literal")]
    [InlineData(@", (literal) @site\item")]
    public async Task Execute_LiteralPlateSuffix_ExcludesKnownRead(string suffix)
    {
        var read = await FindRead();
        var readId = read.GetProperty("ReadId").GetGuid();
        var timestamp = read.GetProperty("TimestampUtc").GetDateTimeOffset();
        var request = new LprSearchRequest(read.GetProperty("Plate").GetString()!,
            timestamp.AddSeconds(-1).UtcDateTime.ToString("O"), timestamp.AddSeconds(1).UtcDateTime.ToString("O"),
            [read.GetProperty("UnitId").GetGuid()], MaximumResultCount: 100);

        using var included = JsonDocument.Parse(Assert.IsType<string>(await LprSearchTool.Execute(sc.Manager, request)));
        Assert.Contains(included.RootElement.EnumerateArray(), row => row.GetProperty("ReadId").GetGuid() == readId);
        using var excluded = JsonDocument.Parse(Assert.IsType<string>(await LprSearchTool.Execute(sc.Manager,
            request with { PlateNumber = request.PlateNumber + suffix })));
        Assert.DoesNotContain(excluded.RootElement.EnumerateArray(), row => row.GetProperty("ReadId").GetGuid() == readId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Execute_WithLimitBounds_ReturnsMatchingReads(int maximumResultCount)
    {
        var read = await FindRead();
        var sourceId = read.GetProperty("UnitId").GetGuid();
        var plate = read.GetProperty("Plate").GetString()!;
        var timestamp = read.GetProperty("TimestampUtc").GetDateTimeOffset();
        var start = timestamp.AddMinutes(-1);
        var end = timestamp.AddMinutes(1);
        var result = await LprSearchTool.Execute(sc.Manager, new LprSearchRequest(
            plate, start.UtcDateTime.ToString("O"), end.UtcDateTime.ToString("O"), [sourceId], maximumResultCount));
        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.InRange(document.RootElement.GetArrayLength(), 1, maximumResultCount);
        foreach (var row in document.RootElement.EnumerateArray())
        {
            Assert.NotEqual(Guid.Empty, row.GetProperty("ReadId").GetGuid());
            Assert.Equal(sourceId, row.GetProperty("UnitId").GetGuid());
            Assert.Equal(plate, row.GetProperty("Plate").GetString());
            Assert.InRange(row.GetProperty("TimestampUtc").GetDateTimeOffset(), start, end);
        }
    }

    [Fact]
    public async Task Execute_WithDateWindows_IncludesAndExcludesKnownRead()
    {
        var read = await FindRead();
        var readId = read.GetProperty("ReadId").GetGuid();
        var timestamp = read.GetProperty("TimestampUtc").GetDateTimeOffset();
        var request = new LprSearchRequest(read.GetProperty("Plate").GetString()!,
            timestamp.AddSeconds(-1).UtcDateTime.ToString("O"), timestamp.AddSeconds(1).UtcDateTime.ToString("O"),
            [read.GetProperty("UnitId").GetGuid()], MaximumResultCount: 100);

        using var included = JsonDocument.Parse(Assert.IsType<string>(await LprSearchTool.Execute(sc.Manager, request)));
        Assert.Contains(included.RootElement.EnumerateArray(), row => row.GetProperty("ReadId").GetGuid() == readId);

        var nextWindow = request with { StartDate = request.EndDate, EndDate = timestamp.AddSeconds(3).UtcDateTime.ToString("O") };
        using var excluded = JsonDocument.Parse(Assert.IsType<string>(await LprSearchTool.Execute(sc.Manager, nextWindow)));
        Assert.DoesNotContain(excluded.RootElement.EnumerateArray(), row => row.GetProperty("ReadId").GetGuid() == readId);
    }

    private async Task<JsonElement> FindRead()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var client = sc.Manager.GetClient();
        var sources = await client.ListEntities("LprUnit", 1, 1);
        var sourceId = IntegrationTestHelpers.ExtractFirstGuid(sources);
        Assert.True(sourceId.HasValue, "An ALPR unit is required for the read-query integration tests.");

        // Use an existing historical read so live captures do not change the test windows.
        var end = DateTimeOffset.UtcNow.AddMinutes(-10);
        var result = await client.RunReport("LprRead", BuildQuery(
            QueryCollection("SourceIds", new[] { sourceId.Value }),
            QueryTimeRange(end.AddDays(-7).UtcDateTime.ToString("O"), end.UtcDateTime.ToString("O")),
            QueryValue("MaximumResultCount", 1)));
        using var document = JsonDocument.Parse(result);
        var read = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(sourceId.Value, read.GetProperty("UnitId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(read.GetProperty("Plate").GetString()),
            "The selected ALPR unit needs an existing plate read for these integration tests.");
        return read.Clone();
    }
}
