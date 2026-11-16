using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter;

public static class ScopedToolFilter
{
    private const string ToolsNamespacePrefix = "Genetec.Mcp.SecurityCenter.Tools.";
    private static readonly Regex CamelCasePattern = new(@"(?<=[a-z])([A-Z])", RegexOptions.Compiled);

    public static ToolScope? DeriveScope(string typeFullName)
    {
        if (!typeFullName.StartsWith(ToolsNamespacePrefix, StringComparison.Ordinal))
            return null;

        var remainder = typeFullName[ToolsNamespacePrefix.Length..];
        if (!remainder.Contains('.')) return null;
        var segment = remainder[..remainder.IndexOf('.')];
        return ParseScope(CamelCasePattern.Replace(segment, "_$1").ToLowerInvariant());
    }

    public static ToolScope? ParseScope(string snake) => snake switch
    {
        "access_control"    => ToolScope.AccessControl,
        "video_surveillance"=> ToolScope.VideoSurveillance,
        "lpr"               => ToolScope.Lpr,
        "events"            => ToolScope.Events,
        "administration"    => ToolScope.Administration,
        "utilities"         => ToolScope.Utilities,
        _                   => null
    };

    public static bool IsScopeAllowed(HashSet<ToolScope> grantedScopes, ToolScope toolScope)
    {
        return grantedScopes.Contains(toolScope);
    }

    public static IReadOnlyDictionary<string, ToolScope> BuildToolScopeMap(Assembly assembly)
    {
        var map = new Dictionary<string, ToolScope>(StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;

            var scope = DeriveScope(type.FullName ?? "");
            if (scope is null) continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr?.Name is { } name)
                    map[name] = scope.Value;
            }
        }

        return map;
    }

    private static readonly JsonElement StubInputSchema =
        JsonDocument.Parse("""{"type":"object","properties":{"request":{"type":"object"}}}""").RootElement.Clone();

    public static McpRequestFilter<ListToolsRequestParams, ListToolsResult> CreateSchemaStripFilter()
    {
        return next => async (context, ct) =>
        {
            var result = await next(context, ct);
            var stripped = result.Tools
                .Select(t => t.Name is "search_tools" or "session_management" ? t : new Tool
                {
                    Name = t.Name,
                    Title = t.Title,
                    Description = t.Description,
                    InputSchema = StubInputSchema,
                    Annotations = t.Annotations,
                    Meta = t.Meta
                })
                .ToList();
            return new ListToolsResult
            {
                Tools = stripped,
                NextCursor = result.NextCursor,
                Meta = result.Meta
            };
        };
    }

    public static McpRequestFilter<ListToolsRequestParams, ListToolsResult> CreateFilter(
        IReadOnlyDictionary<string, ToolScope> toolNameToScope)
    {
        return next => async (context, ct) =>
        {
            var result = await next(context, ct);

            var scopeClaim = context.User?.FindFirst("scope")?.Value ?? "";

            var grantedScopes = scopeClaim
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseScope)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToHashSet();

            return ApplyScopeFilter(result, toolNameToScope, grantedScopes);
        };
    }

    public static McpRequestFilter<ListToolsRequestParams, ListToolsResult> CreateFilter(
        IReadOnlyDictionary<string, ToolScope> toolNameToScope,
        HashSet<ToolScope> allowedScopes)
        => CreateFilter(toolNameToScope, () => allowedScopes);

    public static McpRequestFilter<ListToolsRequestParams, ListToolsResult> CreateFilter(
        IReadOnlyDictionary<string, ToolScope> toolNameToScope,
        Func<HashSet<ToolScope>> getAllowedScopes)
    {
        return next => async (context, ct) =>
        {
            var result = await next(context, ct);
            var allowedScopes = getAllowedScopes();
            if (allowedScopes.Count == 0) return result;
            return ApplyScopeFilter(result, toolNameToScope, allowedScopes);
        };
    }

    private static ListToolsResult ApplyScopeFilter(
        ListToolsResult result,
        IReadOnlyDictionary<string, ToolScope> toolNameToScope,
        HashSet<ToolScope> grantedScopes)
    {
        var filteredTools = result.Tools
            .Where(t =>
            {
                var hasScope = toolNameToScope.TryGetValue(t.Name, out var toolScope);
                return !hasScope || IsScopeAllowed(grantedScopes, toolScope);
            })
            .ToList();

        return new ListToolsResult
        {
            Tools = filteredTools,
            NextCursor = result.NextCursor,
            Meta = result.Meta
        };
    }
}
