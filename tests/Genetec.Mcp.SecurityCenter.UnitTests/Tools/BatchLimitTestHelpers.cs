using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

internal static class BatchLimitTestHelpers
{
    internal static IEnumerable<object[]> Cases(string domain)
    {
        foreach (var type in typeof(SearchToolsTool).Assembly.GetTypes()
                     .Where(type => type.Namespace == $"Genetec.Mcp.SecurityCenter.Tools.{domain}" &&
                                    type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
        {
            var method = type.GetMethod("Execute")!;
            var toolName = method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;
            if (toolName == "camera_thumbnails") continue;
            var requestParameter = method.GetParameters().SingleOrDefault(parameter => parameter.Name == "request");
            if (requestParameter is null) continue;
            var requestType = requestParameter.ParameterType;
            foreach (var path in CollectionPaths(requestType))
                yield return [toolName, path];
        }
    }

    internal static void AssertLimits(string toolName, string path)
    {
        var tool = typeof(SearchToolsTool).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Single(type => type.GetMethod("Execute")?.GetCustomAttribute<McpServerToolAttribute>()?.Name == toolName);
        var requestType = tool.GetMethod("Execute")!.GetParameters()
            .Single(parameter => parameter.Name == "request").ParameterType;
        var request = RequiredObject(requestType);
        var ownerType = requestType;
        var owner = request;
        var parts = path.Split('.');
        for (var index = 0; index < parts.Length - 1; index++)
        {
            var property = ownerType.GetProperty(parts[index])!;
            var isList = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>);
            var innerType = isList ? property.PropertyType.GetGenericArguments()[0] : property.PropertyType;
            owner = RequiredObject(innerType);
            var name = SnakeCase(property.Name);
            var rootOwner = FindOwner(request, parts[..index]);
            rootOwner[name] = isList ? new JsonArray(owner) : owner;
            ownerType = innerType;
        }

        var boundedProperty = ownerType.GetProperty(parts[^1])!;
        var propertyName = SnakeCase(boundedProperty.Name);
        var elementType = boundedProperty.PropertyType.GetGenericArguments()[0];
        JsonArray Values(int count) => new(Enumerable.Range(0, count)
            .Select(_ => RequiredValue(elementType)).ToArray());

        owner[propertyName] = Values(ResourceLimits.MaxBatchItems);
        var bound = JsonSerializer.Deserialize(request.ToJsonString(), requestType, NumericStringConverter.ToolOptions);
        Assert.NotNull(bound);
        owner[propertyName] = Values(ResourceLimits.MaxBatchItems + 1);
        var error = Assert.Throws<ValidationException>(() =>
            JsonSerializer.Deserialize(request.ToJsonString(), requestType, NumericStringConverter.ToolOptions));
        Assert.Contains(boundedProperty.Name, error.Message, StringComparison.Ordinal);

        if (boundedProperty.GetCustomAttribute<RequiredAttribute>() is not null)
        {
            owner.Remove(propertyName);
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize(request.ToJsonString(), requestType, NumericStringConverter.ToolOptions));
            owner[propertyName] = new JsonArray();
            Assert.Throws<ValidationException>(() =>
                JsonSerializer.Deserialize(request.ToJsonString(), requestType, NumericStringConverter.ToolOptions));
        }

        var discovery = SearchToolsTool.Execute(new SearchToolsRequest(Query: toolName, Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(discovery, NumericStringConverter.ToolOptions));
        var discoveredTool = Assert.Single(document.RootElement.EnumerateArray(),
            item => item.GetProperty("name").GetString() == toolName);
        var fields = Assert.Single(discoveredTool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        JsonElement field = default;
        for (var index = 0; index < parts.Length; index++)
        {
            field = Assert.Single(fields.EnumerateArray(), item => item.GetProperty("name").GetString() == SnakeCase(parts[index]));
            if (index < parts.Length - 1) fields = field.GetProperty("fields");
        }
        Assert.Equal(ResourceLimits.MaxBatchItems, field.GetProperty("max_length").GetInt32());
    }

    private static IEnumerable<string> CollectionPaths(Type type)
    {
        foreach (var property in type.GetProperties())
        {
            var isList = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>);
            if (isList)
            {
                Assert.Equal(ResourceLimits.MaxBatchItems, property.GetCustomAttribute<MaxLengthAttribute>()?.Length);
                yield return property.Name;
            }
            var inner = isList ? property.PropertyType.GetGenericArguments()[0] : property.PropertyType;
            if (inner.Namespace?.StartsWith("Genetec.Mcp.SecurityCenter.Tools", StringComparison.Ordinal) == true)
                foreach (var nested in CollectionPaths(inner))
                    yield return $"{property.Name}.{nested}";
        }
    }

    private static JsonObject FindOwner(JsonObject request, string[] parts)
    {
        var owner = request;
        foreach (var part in parts)
        {
            var value = owner[SnakeCase(part)]!;
            owner = value is JsonArray array ? (JsonObject)array[0]! : (JsonObject)value;
        }
        return owner;
    }

    private static JsonObject RequiredObject(Type type)
    {
        var result = new JsonObject();
        foreach (var property in type.GetProperties().Where(property => property.GetCustomAttribute<RequiredAttribute>() is not null))
            result[SnakeCase(property.Name)] = RequiredValue(property.PropertyType);
        return result;
    }

    private static JsonNode RequiredValue(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return JsonValue.Create("2026-10-28T12:00:00Z")!;
        if (type == typeof(Guid)) return JsonValue.Create(Guid.NewGuid())!;
        if (type == typeof(bool)) return JsonValue.Create(true)!;
        if (type == typeof(TimeSpan)) return JsonValue.Create("00:01:00")!;
        if (type.IsEnum) return JsonSerializer.SerializeToNode(Enum.GetValues(type).GetValue(0), type, NumericStringConverter.ToolOptions)!;
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
            return JsonValue.Create(1)!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return new JsonArray(RequiredValue(type.GetGenericArguments()[0]));
        return RequiredObject(type);
    }

    private static string SnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}
