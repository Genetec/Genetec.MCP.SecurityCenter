using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools;

public class SearchToolsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    [Fact]
    public void DetailLevels_ReturnExpectedShapes()
    {
        _ = sc;
        using var namesDocument = JsonSerializer.SerializeToDocument(
            SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Names)), Options);
        using var summaryDocument = JsonSerializer.SerializeToDocument(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera", Detail: ToolDetailLevel.Summary)), Options);
        using var fullDocument = JsonSerializer.SerializeToDocument(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera_update", Detail: ToolDetailLevel.Full)), Options);
        var names = namesDocument.RootElement;
        var summary = summaryDocument.RootElement;
        var full = fullDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, names.ValueKind);
        Assert.NotEmpty(names.EnumerateArray());
        Assert.NotEmpty(summary.EnumerateArray());
        Assert.True(full[0].TryGetProperty("parameters", out var parameters));
        Assert.Equal(JsonValueKind.Array, parameters.ValueKind);
    }

    [Fact]
    public void QueryAndScopeFilters_ReturnOnlyMatchingTools()
    {
        _ = sc;
        using var resultDocument = JsonSerializer.SerializeToDocument(
            SearchToolsTool.Execute(new SearchToolsRequest(
                Query: "delete", Scope: ToolScope.AccessControl, Detail: ToolDetailLevel.Full)), Options);
        var result = resultDocument.RootElement;

        Assert.NotEmpty(result.EnumerateArray());
        Assert.All(result.EnumerateArray(), tool =>
        {
            Assert.Equal("access_control", tool.GetProperty("scope").GetString());
            var searchableText = string.Join(" ",
                tool.GetProperty("name").GetString(),
                tool.GetProperty("title").GetString(),
                tool.GetProperty("description").GetString());
            Assert.Contains("delete", searchableText, StringComparison.OrdinalIgnoreCase);
        });
    }
}
