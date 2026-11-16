using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class LprSearchToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceIds_MissingOrEmpty_FailsValidation(bool empty)
    {
        TestHelpers.AssertRequiredProperty<LprSearchRequest>(nameof(LprSearchRequest.SourceIds));
        var request = new LprSearchRequest("CATZQB", "2026-10-28T04:00:00Z", "2026-10-28T20:27:20Z",
            empty ? [] : null!);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(LprSearchRequest.SourceIds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaximumResultCount_OutOfRange_FailsValidation(int maximumResultCount)
    {
        var request = new LprSearchRequest("CATZQB", "2026-10-28T04:00:00Z", "2026-10-28T20:27:20Z",
            [Guid.NewGuid()], maximumResultCount);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(LprSearchRequest.MaximumResultCount)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Request_BindsSnakeCaseInputsAndAcceptsLimitBounds(int maximumResultCount)
    {
        var sourceId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            plate_number = "CATZQB",
            start_date = "2026-10-28T04:00:00Z",
            end_date = "2026-10-28T20:27:20Z",
            source_ids = new[] { sourceId },
            maximum_result_count = maximumResultCount
        });

        var request = Assert.IsType<LprSearchRequest>(
            JsonSerializer.Deserialize<LprSearchRequest>(json, NumericStringConverter.ToolOptions));

        Assert.Equal("CATZQB", request.PlateNumber);
        Assert.Equal("2026-10-28T04:00:00Z", request.StartDate);
        Assert.Equal("2026-10-28T20:27:20Z", request.EndDate);
        Assert.Equal(sourceId, Assert.Single(request.SourceIds));
        Assert.Equal(maximumResultCount, request.MaximumResultCount);
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true));
    }

    [Fact]
    public void Request_OmittedLimit_BindsDefault50()
    {
        var request = Assert.IsType<LprSearchRequest>(JsonSerializer.Deserialize<LprSearchRequest>(
            """{"plate_number":"CATZQB","start_date":"2026-10-28T04:00:00Z","end_date":"2026-10-28T20:27:20Z","source_ids":["11111111-2222-3333-4444-555555555555"]}""",
            NumericStringConverter.ToolOptions));

        Assert.Equal(50, request.MaximumResultCount);
    }

    [Fact]
    public void Discovery_ExposesSourceRequirementAndLimitBounds()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "lpr_search", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var fields = request.GetProperty("fields").EnumerateArray().ToList();
        var sources = Assert.Single(fields, field => field.GetProperty("name").GetString() == "source_ids");
        var limit = Assert.Single(fields, field => field.GetProperty("name").GetString() == "maximum_result_count");

        Assert.Equal("request", request.GetProperty("name").GetString());
        Assert.True(sources.GetProperty("required").GetBoolean());
        Assert.Equal(1, sources.GetProperty("min_length").GetInt32());
        Assert.Equal(1, limit.GetProperty("min").GetDouble());
        Assert.Equal(100, limit.GetProperty("max").GetDouble());
        Assert.Contains("no page number or continuation token", tool.GetProperty("description").GetString());
    }
}
