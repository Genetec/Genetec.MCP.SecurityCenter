using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public class TokenCostTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private static string Serialize(object result) => JsonSerializer.Serialize(result, Options);

    private static readonly ToolScope[] Scopes =
    [
        ToolScope.AccessControl,
        ToolScope.VideoSurveillance,
        ToolScope.Lpr,
        ToolScope.Events,
        ToolScope.Administration,
        ToolScope.Utilities
    ];

    // 1 token ≈ 4 chars (conservative estimate for JSON)
    private static int ToTokens(string json) => json.Length / 4;

    private static int FullSchemaTokens(ToolScope scope) =>
        ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Scope: scope, Detail: ToolDetailLevel.Full))));

    private static int StrippedTokens(ToolScope scope) =>
        ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Scope: scope, Detail: ToolDetailLevel.Summary))));

    private static int ToolCount(ToolScope? scope = null) =>
        JsonDocument.Parse(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Scope: scope, Detail: ToolDetailLevel.Names))))
            .RootElement.EnumerateArray().Count();

    [Fact]
    public void PrintTokenBreakdownAllScopes()
    {
        const double pricePerMTok = 3.0; // Sonnet ~$3/MTok input

        Console.WriteLine($"{"Scope",-25} {"Tools",5}  {"Full",7}  {"Stripped",8}  {"Saved",6}  {"$/call Full",11}  {"$/call Stripped",15}");
        Console.WriteLine(new string('-', 95));

        foreach (var scope in Scopes)
        {
            var count = ToolCount(scope);
            var full = FullSchemaTokens(scope);
            var stripped = StrippedTokens(scope);
            var pct = (int)(100.0 * (full - stripped) / full);
            var costFull = full / 1_000_000.0 * pricePerMTok;
            var costStripped = stripped / 1_000_000.0 * pricePerMTok;

            Console.WriteLine($"{scope,-25} {count,5}  {full,7}  {stripped,8}  {pct,5}%  ${costFull,9:F4}   ${costStripped,12:F4}");
        }

        // Admin = all scopes combined (worst case: JWT with every scope granted)
        var adminCount = ToolCount();
        var adminFull = ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Full))));
        var adminStripped = ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Summary))));
        var adminPct = (int)(100.0 * (adminFull - adminStripped) / adminFull);

        Console.WriteLine(new string('-', 95));
        Console.WriteLine($"{"ADMIN (all scopes)",-25} {adminCount,5}  {adminFull,7}  {adminStripped,8}  {adminPct,5}%  ${adminFull / 1_000_000.0 * pricePerMTok,9:F4}   ${adminStripped / 1_000_000.0 * pricePerMTok,12:F4}");

        Console.WriteLine();
        Console.WriteLine("* Stripped = parameter schemas removed from tools/list (served via search_tools(detail:\"full\") on demand)");
        Console.WriteLine($"* At 1M calls/month: Full=${adminFull / 1_000_000.0 * pricePerMTok * 1_000_000:F0}/mo  Stripped=${adminStripped / 1_000_000.0 * pricePerMTok * 1_000_000:F0}/mo");

        Assert.True(true);
    }

    // ── Per-scope reduction is meaningful ──

    [Theory]
    [InlineData(ToolScope.AccessControl)]
    [InlineData(ToolScope.VideoSurveillance)]
    [InlineData(ToolScope.Lpr)]
    [InlineData(ToolScope.Events)]
    [InlineData(ToolScope.Administration)]
    [InlineData(ToolScope.Utilities)]
    public void SchemaStrip_ReducesTokensBy50PctOrMore(ToolScope scope)
    {
        var full = FullSchemaTokens(scope);
        var stripped = StrippedTokens(scope);
        var reduction = (double)(full - stripped) / full;
        Assert.True(reduction >= 0.50,
            $"{scope}: expected >=50% reduction, got {reduction:P0} (full={full}, stripped={stripped})");
    }

    [Fact]
    public void SchemaStrip_AdminScope_ReducesTokensBy70PctOrMore()
    {
        var full = ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Full))));
        var stripped = ToTokens(Serialize(SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Summary))));
        var reduction = (double)(full - stripped) / full;
        Assert.True(reduction >= 0.70,
            $"Admin: expected >=70% reduction, got {reduction:P0} (full={full}, stripped={stripped})");
    }
}
