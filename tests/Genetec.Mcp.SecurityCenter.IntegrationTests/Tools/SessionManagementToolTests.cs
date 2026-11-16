using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools;

public class SessionManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task List_ReturnsSessionState()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        using var services = new ServiceCollection().BuildServiceProvider();
        var result = await SessionManagementTool.Execute(sc.Manager, services,
            new SessionManagementRequest(SessionAction.List));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.True(document.RootElement.TryGetProperty("Sessions", out _));
        Assert.True(document.RootElement.TryGetProperty("Active", out _));
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        using var services = new ServiceCollection().BuildServiceProvider();
        var result = await SessionManagementTool.Execute(sc.Manager, services,
            new SessionManagementRequest(SessionAction.Logout));

        Assert.IsType<StatusResult>(result);
    }

    [Fact]
    public async Task Connect_InHttpMode_UsesTheHttpAuthenticationGuard()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        using var services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider();

        var exception = await Record.ExceptionAsync(() => SessionManagementTool.Execute(sc.Manager, services,
            new SessionManagementRequest(SessionAction.Connect)));

        var argument = Assert.IsType<ArgumentException>(exception);
        Assert.Contains("HTTP mode", argument.Message, StringComparison.OrdinalIgnoreCase);
    }
}
