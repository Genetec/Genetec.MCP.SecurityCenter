using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public class SearchToolsToolTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private static string Serialize(object result) => JsonSerializer.Serialize(result, Options);

    // ── Registry population ──

    [Fact]
    public void NoArgs_ReturnsAllTools()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest());
        var tools = JsonDocument.Parse(Serialize(result)).RootElement.EnumerateArray().ToList();
        Assert.True(tools.Count > 100, $"Expected 100+ tools, got {tools.Count}");
    }

    [Fact]
    public void NoArgs_ToolsAreSortedByScopeThenName()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest());
        var tools = JsonDocument.Parse(Serialize(result)).RootElement
            .EnumerateArray()
            .Select(t => (
                Scope: t.TryGetProperty("scope", out var s) ? s.GetString() : null,
                Name: t.GetProperty("name").GetString()!))
            .ToList();

        for (var i = 1; i < tools.Count; i++)
        {
            var prev = tools[i - 1];
            var curr = tools[i];
            var scopeOrder = string.CompareOrdinal(prev.Scope, curr.Scope);
            if (scopeOrder == 0)
                Assert.True(string.CompareOrdinal(prev.Name, curr.Name) <= 0,
                    $"Within same scope, expected '{prev.Name}' before '{curr.Name}'");
            else
                Assert.True(scopeOrder <= 0,
                    $"Expected scope '{prev.Scope}' before '{curr.Scope}'");
        }
    }

    // ── Detail levels ──

    [Fact]
    public void DetailNames_ReturnsStringArray()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Names));
        var array = JsonDocument.Parse(Serialize(result)).RootElement;
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(JsonValueKind.String, array[0].ValueKind);
    }

    [Fact]
    public void DetailSummary_ReturnsSummaryShape()
    {
        // Use a scope filter so every result has a non-null scope
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Scope: ToolScope.AccessControl, Detail: ToolDetailLevel.Summary));
        var first = JsonDocument.Parse(Serialize(result)).RootElement[0];
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("description", out _));
        Assert.True(first.TryGetProperty("scope", out _));
        Assert.False(first.TryGetProperty("parameters", out _));
    }

    [Fact]
    public void DetailFull_ReturnsParametersArray()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Full));
        var first = JsonDocument.Parse(Serialize(result)).RootElement[0];
        Assert.True(first.TryGetProperty("parameters", out var parameters));
        Assert.Equal(JsonValueKind.Array, parameters.ValueKind);
    }

    // ── Keyword filtering ──

    [Fact]
    public void KeywordFilter_Camera_ReturnsCameraTools()
    {
        var names = ParseNames(SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera")));
        Assert.NotEmpty(names);
        Assert.Contains("camera_get", names);
        Assert.Contains("camera_ptz", names);
        Assert.DoesNotContain("camera_delete", names);
        Assert.DoesNotContain("camera_management", names);
    }

    [Fact]
    public void KeywordFilter_AndSemantics_MultipleTermsMustAllMatch()
    {
        var andNames = ParseNames(SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera list")));
        var singleNames = ParseNames(SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera")));
        Assert.Contains("camera_list", andNames);
        Assert.DoesNotContain("camera_management", andNames);
        Assert.True(andNames.Count <= singleNames.Count,
            $"AND should not return more results than a single term (AND={andNames.Count}, single={singleNames.Count})");
    }

    [Fact]
    public void KeywordFilter_OrFallback_PartialMatchReturnsResults()
    {
        var names = ParseNames(SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera zzznomatch")));
        Assert.NotEmpty(names);
        Assert.Contains("camera_get", names);
        Assert.DoesNotContain("camera_management", names);
    }

    [Fact]
    public void KeywordFilter_NoMatch_ReturnsEmptyArray()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "zzznomatchatall"));
        Assert.Empty(ParseNames(result));
    }

    [Fact]
    public void KeywordFilter_EmptyString_ReturnsAllTools()
    {
        var all = JsonDocument.Parse(Serialize(SearchToolsTool.Execute(new SearchToolsRequest()))).RootElement.EnumerateArray().Count();
        var empty = JsonDocument.Parse(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Query: "")))).RootElement.EnumerateArray().Count();
        Assert.Equal(all, empty);
    }

    // ── Scope filtering ──

    [Fact]
    public void ScopeFilter_VideoSurveillance_ReturnsOnlyVideoTools()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Scope: ToolScope.VideoSurveillance, Detail: ToolDetailLevel.Summary));
        var tools = JsonDocument.Parse(Serialize(result)).RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(tools);
        Assert.All(tools, t =>
            Assert.Equal("video_surveillance", t.GetProperty("scope").GetString()));
    }

    // ── Combined filters ──

    [Fact]
    public void Combined_QueryAndScope_BothApplied()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "delete", Scope: ToolScope.AccessControl));
        var tools = JsonDocument.Parse(Serialize(result)).RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(tools);
        Assert.All(tools, t => Assert.Equal("access_control", t.GetProperty("scope").GetString()));
        var names = tools.Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.Contains("cardholder_delete", names);
    }

    // ── Registry contains known tools ──

    [Fact]
    public void Registry_ContainsSearchToolsItself()
    {
        var names = ParseNames(SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Names)));
        Assert.Contains("search_tools", names);
    }

    [Fact]
    public void Registry_FullDetail_ParametersExcludeSessionManager()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera_update", Detail: ToolDetailLevel.Full));
        var tool = JsonDocument.Parse(Serialize(result)).RootElement[0];
        var paramNames = tool.GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.DoesNotContain("sessionManager", paramNames);
        Assert.Contains("request", paramNames);
    }

    // ── Schema strip (parameter detail on demand) ──

    [Fact]
    public void FullDetail_ContainsParametersForKnownTool()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera_update", Detail: ToolDetailLevel.Full));
        var tool = JsonDocument.Parse(Serialize(result)).RootElement[0];
        var parameters = tool.GetProperty("parameters").EnumerateArray().ToList();
        Assert.NotEmpty(parameters);
        Assert.Contains(parameters, p => p.GetProperty("name").GetString() == "request");
    }

    [Fact]
    public void SummaryDetail_ContainsNoParameters()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "camera_update", Detail: ToolDetailLevel.Summary));
        var tool = JsonDocument.Parse(Serialize(result)).RootElement[0];
        Assert.False(tool.TryGetProperty("parameters", out _));
    }

    // ── Helpers ──

    [Fact]
    public void FullDetail_SessionManagementIncludesRequiredAction()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "session_management", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(Serialize(result));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        Assert.Equal("request", request.GetProperty("name").GetString());
        var action = Assert.Single(request.GetProperty("fields").EnumerateArray());
        Assert.Equal("action", action.GetProperty("name").GetString());
        Assert.True(action.GetProperty("required").GetBoolean());
        Assert.Equal(["list", "connect", "logout"],
            action.GetProperty("enum_values").EnumerateArray().Select(value => value.GetString()));
    }

    private static List<string> ParseNames(object result)
    {
        var root = JsonDocument.Parse(Serialize(result)).RootElement;
        var items = root.EnumerateArray().ToList();
        if (items.Count == 0) return [];
        if (items[0].ValueKind == JsonValueKind.String)
            return items.Select(e => e.GetString()!).ToList();
        return items.Select(e => e.GetProperty("name").GetString()!).ToList();
    }
}
