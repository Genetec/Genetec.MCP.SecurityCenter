using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools;

public class SessionManagementToolTests
{
    private readonly IServiceProvider services = Substitute.For<IServiceProvider>();
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsSessionsShape()
    {
        var result = await SessionManagementTool.Execute(
            TestHelpers.FakeManager(),
            services,
            new SessionManagementRequest(SessionAction.List));

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("sessions", out _));
        Assert.True(doc.RootElement.TryGetProperty("active", out _));
    }

    [Fact]
    public async Task Logout_Succeeds()
    {
        var result = await SessionManagementTool.Execute(
            TestHelpers.FakeManager(),
            services,
            new SessionManagementRequest(SessionAction.Logout));

        Assert.IsType<StatusResult>(result);
    }

    [Fact]
    public async Task Connect_InHttpMode_ThrowsArgumentException()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        services.GetService(typeof(IHttpContextAccessor)).Returns(httpContextAccessor);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SessionManagementTool.Execute(
                TestHelpers.FakeManager(),
                services,
                new SessionManagementRequest(SessionAction.Connect)));

        Assert.Contains("HTTP mode", ex.Message);
    }

    [Fact]
    public async Task Connect_WithoutBrowserSignInService_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SessionManagementTool.Execute(TestHelpers.FakeManager(), services,
                new SessionManagementRequest(SessionAction.Connect)));
        Assert.Contains("Browser sign-in", ex.Message);
    }
}
