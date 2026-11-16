using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class GetToolRequestSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void EveryRegisteredTool_UsesSingleSealedRequestForBusinessInputs()
    {
        var methods = typeof(SearchToolsTool).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null).ToArray();
        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            var name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name;
            var inputs = method.GetParameters().Where(parameter =>
                parameter.ParameterType != typeof(SecurityCenterSessionManager) &&
                parameter.ParameterType != typeof(IServiceProvider) &&
                parameter.ParameterType != typeof(IHttpContextAccessor) &&
                parameter.ParameterType != typeof(StdioSignInService) &&
                parameter.ParameterType != typeof(CancellationToken) &&
                !(parameter.Name == "sessionAlias" && parameter.ParameterType == typeof(string))).ToArray();
            if (name is "system_configuration" or "assurance_profile_list")
            {
                Assert.Empty(inputs);
                continue;
            }

            Assert.True(inputs.Length == 1, $"{name} must expose one request, found {inputs.Length} business parameters.");
            var request = inputs[0];
            Assert.True(request.Name == "request", $"{name} exposes flat parameter '{request.Name}'.");
            Assert.True(request.ParameterType.Name.EndsWith("Request", StringComparison.Ordinal),
                $"{name} must use a *Request type.");
            Assert.True(request.ParameterType.IsSealed && request.ParameterType.GetMethod("<Clone>$") is not null,
                $"{name} request must be a sealed record.");
            Assert.False(string.IsNullOrWhiteSpace(request.GetCustomAttribute<DescriptionAttribute>()?.Description),
                $"{name} request must have a description to be discoverable.");
        }
    }

    [Fact]
    public void FullDetail_EveryInputfulToolExposesItsRequest()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));
        foreach (var tool in document.RootElement.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString();
            var parameters = tool.GetProperty("parameters").EnumerateArray().ToArray();
            if (name is "system_configuration" or "assurance_profile_list")
            {
                Assert.Empty(parameters);
                continue;
            }

            Assert.True(parameters.Length == 1, $"{name} discovery must expose exactly one request.");
            Assert.Equal("request", parameters[0].GetProperty("name").GetString());
            Assert.NotEmpty(parameters[0].GetProperty("fields").EnumerateArray());
        }
    }

    [Theory]
    [InlineData("alarm_get")]
    [InlineData("role_get")]
    [InlineData("user_get")]
    [InlineData("user_group_get")]
    public void FullDetail_ExposesRequiredGuidInsideRequest(string toolName)
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: toolName, Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(toolName, tool.GetProperty("name").GetString());
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        Assert.Equal("request", request.GetProperty("name").GetString());
        Assert.True(request.GetProperty("required").GetBoolean());
        var id = Assert.Single(request.GetProperty("fields").EnumerateArray());
        Assert.Equal("id", id.GetProperty("name").GetString());
        Assert.Equal("Guid", id.GetProperty("type").GetString());
        Assert.True(id.GetProperty("required").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(id.GetProperty("description").GetString()));
    }
}
