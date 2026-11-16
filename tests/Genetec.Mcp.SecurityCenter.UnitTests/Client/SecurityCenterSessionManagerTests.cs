using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using System.Net;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterSessionManagerTests
{
    [Theory]
    [InlineData(401, "Invalid Security Center credentials")]
    [InlineData(403, "denied access")]
    [InlineData(404, "endpoint was not found")]
    [InlineData(408, "did not respond in time")]
    [InlineData(504, "did not respond in time")]
    [InlineData(429, "too many requests")]
    [InlineData(400, "rejected the connection request")]
    [InlineData(500, "server error (HTTP 500)")]
    [InlineData(503, "server error (HTTP 503)")]
    [InlineData(409, "connection request (HTTP 409)")]
    public async Task ValidateConnection_ClassifiesHttpFailuresWithoutExposingResponseBody(int status, string expected)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent("sensitive-server-details")
            }
        };
        var manager = TestHelpers.BuildManager(handler);

        var result = await manager.ValidateAndConnect("user", "password", "localhost");

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
        Assert.DoesNotContain("sensitive-server-details", result.Error);
        Assert.False(manager.HasSession());
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, "hostname could not be resolved")]
    [InlineData(HttpRequestError.ConnectionError, "Could not reach")]
    [InlineData(HttpRequestError.SecureConnectionError, "SSL/TLS handshake failed")]
    [InlineData(HttpRequestError.InvalidResponse, "invalid or incomplete HTTP response")]
    [InlineData(HttpRequestError.ResponseEnded, "invalid or incomplete HTTP response")]
    [InlineData(HttpRequestError.HttpProtocolError, "invalid or incomplete HTTP response")]
    [InlineData(HttpRequestError.Unknown, "Could not communicate")]
    public async Task ValidateConnection_ClassifiesTransportFailures(HttpRequestError error, string expected)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => throw new HttpRequestException(error, "sensitive-exception-details")
        };
        var manager = TestHelpers.BuildManager(handler);

        var result = await manager.ValidateConnection("user", "password", "localhost");

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
        Assert.DoesNotContain("sensitive-exception-details", result.Error);
        Assert.DoesNotContain("Invalid Security Center credentials", result.Error);
        Assert.False(manager.HasSession());
    }

    [Theory]
    [InlineData(true, "HTTPS")]
    [InlineData(false, "HTTP")]
    public async Task ValidateConnection_ConnectionFailureExplainsConfiguredProtocol(bool useHttps, string protocol)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => throw new HttpRequestException(HttpRequestError.ConnectionError)
        };
        var manager = TestHelpers.BuildManager(handler, new ConnectionOptions { UseHttps = useHttps });

        var result = await manager.ValidateConnection("user", "password", "localhost");

        Assert.Contains($"port is configured for {protocol} and", result.Error);
        Assert.Equal(protocol.ToLowerInvariant(), Assert.Single(handler.Requests).Scheme);
    }

    [Theory]
    [InlineData("{\"error\":true,\"message\":\"sensitive-server-details\"}", "rejected the sign-in request")]
    [InlineData("{\"Rsp\":{\"Status\":\"Fail\",\"Result\":\"sensitive-server-details\"}}", "could not complete the sign-in request")]
    [InlineData("<html>sensitive-server-details</html>", "unexpected response")]
    public async Task ValidateConnection_ClassifiesWebSdkAndUnexpectedResponses(string body, string expected)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
        };

        var result = await TestHelpers.BuildManager(handler).ValidateConnection("user", "password", "localhost");

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
        Assert.DoesNotContain("sensitive-server-details", result.Error);
        Assert.DoesNotContain("Invalid Security Center credentials", result.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateConnection_ClassifiesTimeoutAndUnexpectedExceptions(bool timeout)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => throw (timeout
                ? new TaskCanceledException("sensitive-exception-details", new TimeoutException())
                : new Exception("sensitive-exception-details"))
        };

        var result = await TestHelpers.BuildManager(handler).ValidateConnection("user", "password", "localhost");

        Assert.False(result.Success);
        Assert.Contains(timeout ? "did not respond in time" : "Could not complete Security Center sign-in", result.Error);
        Assert.DoesNotContain("sensitive-exception-details", result.Error);
    }

    [Theory]
    [InlineData("{\"error\":false}")]
    [InlineData("{\"Rsp\":{\"Status\":\"Ok\"}}")]
    public async Task ValidateAndConnect_SuccessfulResponseCreatesSession(string body)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
        };
        var manager = TestHelpers.BuildManager(handler);

        var result = await manager.ValidateAndConnect("user", "password", "localhost");

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.True(manager.HasSession());
    }

    private static ScConnectionContext DefaultContext() => new()
    {
        Host = "localhost",
        User = "admin",
        Password = "pass",
    };

    // ── Connect / Disconnect / Active ───────────────────────────────

    [Fact]
    public void Connect_CreatesSessionAndSetsActive()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("test", DefaultContext());
        Assert.Equal("test", manager.ActiveAlias);
        Assert.NotNull(manager.GetClient());
    }

    [Fact]
    public void Connect_MultipleSessions_LastIsActive()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("first", DefaultContext());
        manager.Connect("second", DefaultContext());
        Assert.Equal("second", manager.ActiveAlias);
    }

    [Fact]
    public void Disconnect_RemovesSession()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("test", DefaultContext());
        manager.Disconnect("test");
        Assert.Null(manager.ActiveAlias);
        Assert.Throws<InvalidOperationException>(() => manager.GetClient());
    }

    [Fact]
    public void SetActive_ChangesActiveSession()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("first", DefaultContext());
        manager.Connect("second", DefaultContext());
        manager.SetActive("first");
        Assert.Equal("first", manager.ActiveAlias);
    }

    // ── GetClient ───────────────────────────────────────────────────

    [Fact]
    public void GetClient_ConnectedSession_ReturnsSessionWithCorrectBaseUrl()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("default", new ScConnectionContext { Host = "host1" });

        var client = manager.GetClient();

        Assert.Contains("host1", client.Session.BaseUrl);
    }

    [Fact]
    public void GetClient_NoSession_ThrowsInvalidOperation()
    {
        var manager = TestHelpers.BuildManager();
        Assert.Throws<InvalidOperationException>(() => manager.GetClient());
    }

    // ── ListSessions / HasSession ───────────────────────────────────

    [Fact]
    public void ListSessions_ReturnsAllAliases()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("alpha", DefaultContext());
        manager.Connect("beta", DefaultContext());

        var sessions = manager.ListSessions();

        Assert.Contains("alpha", sessions);
        Assert.Contains("beta", sessions);
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void HasSession_BeforeConnect_ReturnsFalse()
    {
        var manager = TestHelpers.BuildManager();
        Assert.False(manager.HasSession());
    }

    [Fact]
    public void HasSession_AfterConnect_ReturnsTrue()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("test", DefaultContext());
        Assert.True(manager.HasSession());
    }

    [Fact]
    public void HasSession_AfterDisconnectAll_ReturnsFalse()
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("test", DefaultContext());
        manager.Disconnect("test");
        Assert.False(manager.HasSession());
    }

    // ── SkipCertificateValidation ───────────────────────────────────
    // BuildCertificateValidator was removed — TLS behaviour is now configured
    // in DependencyInjection.AddSecurityCenterHttpClient. We verify here that
    // SkipCertificateValidation = true still produces a usable client.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetClient_SkipCertValidation_DoesNotThrow(bool skip)
    {
        var manager = TestHelpers.BuildManager();
        manager.Connect("test", new ScConnectionContext
        {
            Host = "localhost",
            SkipCertificateValidation = skip
        });

        var client = manager.GetClient();

        Assert.NotNull(client);
    }
}
