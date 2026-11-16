using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools;

public class ToolDiscoveryIntegrationTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private McpClient client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        builder.Services.AddOptions<SecurityCenterOptions>();
        builder.Services.AddHttpClient(SecurityCenterSessionManager.HttpClientName);
        builder.Services.AddSingleton<SecurityCenterSessionManager>();
        builder.Services.AddSingleton<StdioSignInService>();
        builder.Services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(SearchToolsTool).Assembly, NumericStringConverter.ToolOptions);
        app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"{address}/mcp")
        }));
    }

    [Theory]
    [InlineData("access_rule_get", "id", "Guid")]
    [InlineData("area_get", "id", "Guid")]
    [InlineData("cardholder_get", "id", "Guid")]
    [InlineData("credential_get", "id", "Guid")]
    [InlineData("door_get", "id", "Guid")]
    [InlineData("visitor_get", "id", "Guid")]
    [InlineData("camera_get", "id", "Guid")]
    [InlineData("video_unit_get", "id", "Guid")]
    [InlineData("alarm_get", "id", "Guid")]
    [InlineData("role_get", "id", "Guid")]
    [InlineData("user_get", "id", "Guid")]
    [InlineData("user_group_get", "id", "Guid")]
    [InlineData("session_management", "action", "SessionAction")]
    public async Task FullDiscovery_ExposesRequiredRequestFieldOverMcpHttp(
        string toolName, string fieldName, string fieldType)
    {
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == toolName);

        var result = await client.CallToolAsync("search_tools", new Dictionary<string, object?>
        {
            ["request"] = new { query = toolName, detail = "full" }
        });
        Assert.False(result.IsError == true);
        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        using var document = JsonDocument.Parse(content.Text);
        var discoveredTool = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(toolName, discoveredTool.GetProperty("name").GetString());
        var request = Assert.Single(discoveredTool.GetProperty("parameters").EnumerateArray());
        Assert.Equal("request", request.GetProperty("name").GetString());
        Assert.True(request.GetProperty("required").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(request.GetProperty("description").GetString()));
        var field = Assert.Single(request.GetProperty("fields").EnumerateArray());
        Assert.Equal(fieldName, field.GetProperty("name").GetString());
        Assert.Equal(fieldType, field.GetProperty("type").GetString());
        Assert.True(field.GetProperty("required").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(field.GetProperty("description").GetString()));
    }

    public async Task DisposeAsync()
    {
        if (client is not null)
            await client.DisposeAsync();
        if (app is not null)
            await app.DisposeAsync();
    }
}
