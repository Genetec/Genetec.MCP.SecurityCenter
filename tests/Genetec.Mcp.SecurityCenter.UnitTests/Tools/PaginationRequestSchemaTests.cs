using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public class PaginationRequestSchemaTests
{
    public static IEnumerable<object[]> RequestTypes()
        => typeof(SearchToolsTool).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Genetec.Mcp.SecurityCenter.Tools", StringComparison.Ordinal) == true && type.GetProperty("PageSize") is not null)
            .OrderBy(type => type.Name).Select(type => new object[] { type });

    [Fact]
    public void PagingInventory_CoversAll48ToolSchemas()
        => Assert.Equal(48, RequestTypes().Count());

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void PageSize_UsesProductionBindingAnd100RecordCeiling(Type requestType)
    {
        var property = requestType.GetProperty("PageSize")!;
        var range = Assert.IsType<RangeAttribute>(property.GetCustomAttribute<RangeAttribute>());
        Assert.Equal(1, range.Minimum);
        Assert.Equal(ResourceLimits.MaxPageSize, range.Maximum);
        Assert.Contains("100", property.GetCustomAttribute<DescriptionAttribute>()!.Description);

        var json = MinimalRequest(requestType);
        foreach (var size in new[] { 1, 100 })
        {
            json["page_size"] = size;
            var request = JsonSerializer.Deserialize(json.ToJsonString(), requestType, NumericStringConverter.ToolOptions)!;
            Assert.Equal(size, property.GetValue(request));
        }
        foreach (var size in new[] { -1, 0, 101 })
        {
            json["page_size"] = size;
            Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize(json.ToJsonString(), requestType, NumericStringConverter.ToolOptions));
        }
        json.Remove("page_size");
        var defaults = JsonSerializer.Deserialize(json.ToJsonString(), requestType, NumericStringConverter.ToolOptions)!;
        Assert.Equal(requestType == typeof(UserGroupReportFilters) ? null : ResourceLimits.DefaultPageSize, property.GetValue(defaults));
    }

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void Discovery_Exposes100RecordPageLimit(Type requestType)
    {
        var ownerType = requestType == typeof(UserGroupReportFilters) ? typeof(ReportGeneralRequest) : requestType;
        var method = typeof(SearchToolsTool).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null && method.GetParameters().Any(parameter => parameter.ParameterType == ownerType));
        var name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            SearchToolsTool.Execute(new SearchToolsRequest(Query: name, Detail: ToolDetailLevel.Full)), NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), tool => tool.GetProperty("name").GetString() == name);
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var pageSize = Assert.Single(FindFields(request.GetProperty("fields")), field => field.GetProperty("name").GetString() == "page_size");
        Assert.Equal(1, pageSize.GetProperty("min").GetInt32());
        Assert.Equal(ResourceLimits.MaxPageSize, pageSize.GetProperty("max").GetInt32());
        Assert.Contains("100", pageSize.GetProperty("description").GetString());
    }

    private static JsonObject MinimalRequest(Type type)
    {
        var json = new JsonObject();
        foreach (var property in type.GetProperties().Where(property => property.GetCustomAttribute<RequiredAttribute>() is not null))
            json[JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name)] = property.PropertyType.IsEnum ? JsonValue.Create(0)
                : property.PropertyType == typeof(Guid) ? JsonValue.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"))
                : JsonValue.Create("value");
        return json;
    }

    private static IEnumerable<JsonElement> FindFields(JsonElement fields)
    {
        foreach (var field in fields.EnumerateArray())
        {
            yield return field;
            if (field.TryGetProperty("fields", out var nested) && nested.ValueKind == JsonValueKind.Array)
                foreach (var child in FindFields(nested)) yield return child;
        }
    }
}
