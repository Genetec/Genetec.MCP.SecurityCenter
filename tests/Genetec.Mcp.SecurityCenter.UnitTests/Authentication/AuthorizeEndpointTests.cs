using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class AuthorizeEndpointTests
{
    [Theory]
    [InlineData(HttpRequestError.ConnectionError, "Could not reach")]
    [InlineData(HttpRequestError.SecureConnectionError, "SSL/TLS handshake failed")]
    [InlineData(HttpRequestError.NameResolutionError, "hostname could not be resolved")]
    public async Task ValidateLoginForm_PreservesConnectionFailureInRenderedPage(HttpRequestError failure, string expected)
    {
        var handler = new ToolTestHandler
        {
            ResponseFactory = _ => throw new HttpRequestException(failure, "sensitive-exception-details")
        };
        var options = new SecurityCenterOptions
        {
            WebSdkSettings = new ConnectionOptions { Hosts = ["localhost"] }
        };
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["host"] = "localhost", ["username"] = "user", ["password"] = "password", ["scope"] = "utilities"
        });

        var (connection, error) = await AuthorizeEndpoint.ValidateLoginForm(form, options, TestHelpers.BuildManager(handler));

        Assert.Null(connection);
        Assert.NotNull(error);
        Assert.Contains(expected, error);
        var page = AuthorizeEndpoint.BuildLoginPage("", "", "", "", "", "utilities", errorMessage: error);
        Assert.Contains(WebUtility.HtmlEncode(error), page);
        Assert.DoesNotContain("Invalid Security Center credentials", page);
        Assert.DoesNotContain("sensitive-exception-details", page);
    }

    [Theory]
    [InlineData("allowed.example.com", true)]
    [InlineData("ALLOWED.EXAMPLE.COM", true)]
    [InlineData("::1", true)]
    [InlineData("child.allowed.example.com", false)]
    [InlineData("allowed.example.com.attacker.example", false)]
    [InlineData("https://allowed.example.com", false)]
    public void IsAllowedHost_RequiresExactHostnameMatch(string host, bool expected)
    {
        var options = new SecurityCenterOptions
        {
            WebSdkSettings = new ConnectionOptions { Hosts = [" allowed.example.com ", "[::1]"] }
        };

        Assert.Equal(expected, AuthorizeEndpoint.IsAllowedHost(host, options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ValidateLoginForm_RejectsUnlistedHostBeforeConnecting(int allowlistSize)
    {
        var options = new SecurityCenterOptions
        {
            WebSdkSettings = new ConnectionOptions
            {
                Hosts = new[] { "allowed.example.com", "second.example.com" }.Take(allowlistSize).ToArray()
            }
        };
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["host"] = "unlisted.example.com",
            ["username"] = "test",
            ["password"] = "test",
            ["scope"] = "access_control"
        });

        // A rejected host must not use the connection manager at all.
        var (connection, error) = await AuthorizeEndpoint.ValidateLoginForm(form, options, null!);

        Assert.Null(connection);
        Assert.NotNull(error);
        Assert.DoesNotContain("allowed.example.com", error);
        Assert.DoesNotContain("second.example.com", error);
    }

    [Fact]
    public async Task FormSelectedHost_PersistsThroughSessionKeyAndProtectedContextRestoration()
    {
        var options = new SecurityCenterOptions
        {
            WebSdkSettings = new ConnectionOptions
            {
                Hosts = ["first.example.com", "second.example.com", "third.example.com"],
                Port = 4590,
                User = "configured-user",
                Password = "configured-password"
            }
        };
        var handler = new ToolTestHandler();
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton(new SessionContextProtector(Options.Create(new AuthenticationOptions
        {
            SigningKey = new string('k', 32)
        })));
        services.AddHttpClient(SecurityCenterSessionManager.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var manager = new SecurityCenterSessionManager(
            Options.Create(options), provider.GetRequiredService<IHttpClientFactory>(), provider);
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["host"] = "second.example.com:4592",
            ["username"] = "browser-user",
            ["password"] = "browser-password",
            ["scope"] = "access_control"
        });

        var (connection, error) = await AuthorizeEndpoint.ValidateLoginForm(form, options, manager);

        Assert.Null(error);
        Assert.NotNull(connection);
        Assert.Equal("second.example.com", connection.Host);
        Assert.Equal(4592, connection.Port);
        Assert.Equal("browser-user", connection.User);
        Assert.Equal("browser-password", connection.Password);
        var result = await manager.ValidateAndConnect(connection.User, connection.Password, connection.Host, connection.Port);
        Assert.True(result.Success);
        Assert.Equal("browser-user@second.example.com:4592", manager.CurrentClientId);
        Assert.Equal("https://second.example.com:4592/WebSdk/", manager.GetClient().Session.BaseUrl);
        Assert.All(handler.Requests, request => Assert.Equal("second.example.com", request.Host));

        var protector = provider.GetRequiredService<SessionContextProtector>();
        var protectedContext = protector.Protect(connection);
        options.WebSdkSettings.Hosts = ["third.example.com", "first.example.com", "second.example.com"];
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "oauth-client"), new Claim("sc_ctx", protectedContext)], "test"))
        };
        var restoredManager = new SecurityCenterSessionManager(
            Options.Create(options), provider.GetRequiredService<IHttpClientFactory>(), provider);

        Assert.Equal("second.example.com", protector.Unprotect(protectedContext)!.Host);
        Assert.Equal("https://second.example.com:4592/WebSdk/", restoredManager.GetClient().Session.BaseUrl);
        Assert.Equal(manager.GetClient().Session.AuthHeader, restoredManager.GetClient().Session.AuthHeader);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, false)]
    public void BuildLoginPage_ShowsWarningsForEffectiveWebSdkSecuritySettings(
        bool useHttps, bool skipCertificateValidation, bool httpWarning, bool certificateWarning)
    {
        var page = AuthorizeEndpoint.BuildLoginPage("", "", "", "", "", "",
            connectionOptions: new ConnectionOptions
            {
                UseHttps = useHttps,
                SkipCertificateValidation = skipCertificateValidation
            });

        Assert.Contains("AI agent access", page);
        Assert.Contains("Genetec disclaims liability", page);
        Assert.Contains("browserHttpWarning", page);
        Assert.Equal(httpWarning, page.Contains("Security Center connection is not secure"));
        Assert.Equal(certificateWarning, page.Contains("Security Center identity is not verified"));
        Assert.DoesNotContain("{{CONNECTION_WARNINGS}}", page);
    }

    [Fact]
    public void BuildLoginPage_StdioOmitsBrowserHttpWarning()
    {
        var page = AuthorizeEndpoint.BuildLoginPage("", "", "", "", "", "",
            showBrowserHttpWarning: false);

        Assert.DoesNotContain("id=\"browserHttpWarning\"", page);
        Assert.DoesNotContain("{{BROWSER_HTTP_WARNING}}", page);
        Assert.Contains("AI agent access", page);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostAuthorize_ThrottleStartupSwitch_PreservesValidationAndGet(bool enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = enabled.ToString(),
            ["RateLimiting:AuthorizeWindowAttemptLimit"] = "2",
            ["RateLimiting:AuthorizeWindowSeconds"] = "60"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        if (enabled)
        {
            builder.Services.AddThrottling(builder.Configuration);
            builder.Services.AddAuthorizationThrottling();
        }
        else
            builder.Services.AddRateLimitingOptions(builder.Configuration);
        await using var app = builder.Build();
        app.UseRouting();
        if (enabled)
            app.UseRateLimiter();
        app.MapAuthorizeEndpoint();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        // Invalid submissions return before any Security Center services are resolved.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>());
            using var response = await client.PostAsync("/authorize", form);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var rejectedForm = new FormUrlEncodedContent(new Dictionary<string, string>());
        using var rejected = await client.PostAsync("/authorize", rejectedForm);
        Assert.Equal(enabled ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, rejected.StatusCode);
        if (enabled)
            Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        else
            Assert.Null(rejected.Headers.RetryAfter);

        Assert.Equal(enabled, app.Services.GetService<ThrottleService>() is not null);
        var postEndpoint = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Single(endpoint => endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);
        var metadata = Assert.IsType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>(
            postEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>());
        Assert.Equal(AuthorizeEndpoint.RateLimitPolicy, metadata.PolicyName);

        using var get = await client.GetAsync("/authorize");
        Assert.Equal(HttpStatusCode.BadRequest, get.StatusCode);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(10, 0)]
    [InlineData(-1, 60)]
    [InlineData(10, -1)]
    public void AuthorizationThrottle_InvalidLimitsFailValidation(int attempts, int seconds)
    {
        var result = new RateLimitingOptionsValidator().Validate(null, new RateLimitingOptions
        {
            AuthorizeWindowAttemptLimit = attempts,
            AuthorizeWindowSeconds = seconds
        });
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("video_surveillance access_control", "video_surveillance access_control")]
    [InlineData("video_surveillance unknown_scope", "video_surveillance")]
    [InlineData("unknown_scope", "")]
    [InlineData("", "")]
    [InlineData("lpr events administration utilities", "lpr events administration utilities")]
    public void NormalizeScope_FiltersUnknownValues(string input, string expected)
    {
        var result = AuthorizeEndpoint.NormalizeScope(input);
        Assert.Equal(expected, result);
    }
}
