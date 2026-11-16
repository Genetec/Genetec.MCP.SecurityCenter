using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools;

[McpServerToolType]
public class SearchToolsTool
{
    private sealed record ToolEntry(
        string Name,
        string Title,
        string Description,
        ToolScope? Scope,
        bool ReadOnly,
        IReadOnlyList<ParameterEntry> Parameters);

    private sealed record ParameterEntry(
        string Name,
        string Type,
        string Description,
        bool Required,
        IReadOnlyList<FieldEntry>? Fields = null);

    private sealed record FieldEntry(
        string Name,
        string Type,
        string? Description,
        bool Required,
        IReadOnlyList<string>? EnumValues = null,
        IReadOnlyList<FieldEntry>? Fields = null,
        int? MinLength = null,
        int? MaxLength = null,
        double? Min = null,
        double? Max = null);

    private static readonly IReadOnlyList<ToolEntry> Registry = BuildRegistry();

    [McpServerTool(
        Name = "search_tools",
        Title = "Search Tools",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false),
     Description(
        "Discover available MCP tools without loading all 140+ schemas upfront. " +
        "Use this before calling tools to find the right one for the task. " +
        "Recommended flow: detail='names' for a cheap overview, 'summary' to read descriptions, " +
        "'full' for the parameter schema of the specific tool you need.")]
    public static object Execute(
        [Description("Search tools request")] SearchToolsRequest request)
    {
        var results = Filter(request.Query, request.Scope);

        return request.Detail switch
        {
            ToolDetailLevel.Names => (object)results.Select(t => t.Name).ToList(),
            ToolDetailLevel.Full  => results,
            _                     => results.Select(t => new { t.Name, t.Scope, t.Description }).ToList()
        };
    }
    
    private static IReadOnlyList<ToolEntry> Filter(string? query, ToolScope? scope)
    {
        IEnumerable<ToolEntry> candidates = Registry;

        if (scope.HasValue)
            candidates = candidates.Where(t => t.Scope == scope.Value);

        if (string.IsNullOrWhiteSpace(query))
            return candidates.ToList();

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var andResults = candidates.Where(t => terms.All(term => Matches(t, term))).ToList();
        if (andResults.Count > 0)
            return andResults;

        return candidates
            .Select(t => (Tool: t, Score: terms.Count(term => Matches(t, term))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Tool)
            .ToList();
    }

    private static bool Matches(ToolEntry t, string term) =>
        t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        t.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        t.Description.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ToolEntry> BuildRegistry()
    {
        var tools = new List<ToolEntry>();

        foreach (var type in typeof(SearchToolsTool).Assembly.GetTypes().OrderBy(t => t.FullName))
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;

            var scope = ScopedToolFilter.DeriveScope(type.FullName ?? "");

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr?.Name is not { } name) continue;

                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

                var parameters = method.GetParameters()
                    .Where(p => p.GetCustomAttribute<DescriptionAttribute>() is not null)
                    .Select(p => new ParameterEntry(
                        p.Name ?? "",
                        GetTypeName(p.ParameterType),
                        p.GetCustomAttribute<DescriptionAttribute>()!.Description,
                        !p.HasDefaultValue,
                        ExpandFields(p.ParameterType)))
                    .ToList();

                tools.Add(new ToolEntry(name, toolAttr.Title ?? name, description, scope, toolAttr.ReadOnly, parameters));
            }
        }

        return tools.OrderBy(t => t.Scope?.ToString()).ThenBy(t => t.Name).ToList();
    }

    private static string GetTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        var t = underlying ?? type;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            return $"List<{GetTypeName(t.GetGenericArguments()[0])}>";
        return t.Name;
    }

    private static T? GetAttr<T>(PropertyInfo prop, ParameterInfo? ctorParam) where T : Attribute =>
        prop.GetCustomAttribute<T>() ?? ctorParam?.GetCustomAttribute<T>();

    private static IReadOnlyList<FieldEntry>? ExpandFields(Type type, int depth = 0)
    {
        if (depth > 3) return null;

        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))
            return ExpandFields(underlying.GetGenericArguments()[0], depth);

        if (underlying.IsEnum)
            return null;

        if (!underlying.IsClass && !underlying.IsValueType) return null;
        if (underlying.Namespace?.StartsWith("System") == true) return null;

        var props = underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length == 0) return null;

        var ctorParams = underlying.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var fields = new List<FieldEntry>();
        foreach (var prop in props)
        {
            ctorParams.TryGetValue(prop.Name, out var ctorParam);

            var desc = GetAttr<DescriptionAttribute>(prop, ctorParam)?.Description;
            var required = GetAttr<RequiredAttribute>(prop, ctorParam) is not null;

            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var listInner = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>)
                ? propType.GetGenericArguments()[0] : null;
            var leafType = listInner ?? propType;

            var enumValues = leafType.IsEnum
                ? Enum.GetNames(leafType).Select(n => JsonNamingPolicy.SnakeCaseLower.ConvertName(n)).ToList()
                : null;

            var minLenAttr = GetAttr<MinLengthAttribute>(prop, ctorParam);
            var maxLenAttr = GetAttr<MaxLengthAttribute>(prop, ctorParam);
            var strLenAttr = GetAttr<StringLengthAttribute>(prop, ctorParam);
            var rangeAttr  = GetAttr<RangeAttribute>(prop, ctorParam);

            int? minLength = minLenAttr?.Length ?? (strLenAttr?.MinimumLength > 0 ? strLenAttr.MinimumLength : null);
            int? maxLength = maxLenAttr?.Length ?? strLenAttr?.MaximumLength;
            double? min = rangeAttr?.Minimum is { } rmin ? Convert.ToDouble(rmin) : null;
            double? max = rangeAttr?.Maximum is { } rmax ? Convert.ToDouble(rmax) : null;

            fields.Add(new FieldEntry(
                JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name),
                GetTypeName(prop.PropertyType),
                desc,
                required,
                enumValues,
                ExpandFields(prop.PropertyType, depth + 1),
                minLength,
                maxLength,
                min,
                max));
        }
        return fields;
    }
}

public sealed record SearchToolsRequest(
    [Description("Space-separated keywords to match against tool name and description (e.g. 'camera recording', 'create cardholder'). All terms must match; falls back to any-term if no results.")] string? Query = null,
    [Description("Restrict results to one domain")] ToolScope? Scope = null,
    [Description("How much detail to return: names (cheapest), summary (default), full (adds parameter schemas)")] ToolDetailLevel Detail = ToolDetailLevel.Summary);
