using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class OAuthBoundaryTests
{
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private const string SigningKey = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
    private static string Challenge => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Theory]
    [InlineData(true, "utilities", "access_control video_surveillance")]
    [InlineData(false, "openid profile access_control", "access_control")]
    [InlineData(false, "openid profile", "access_control administration events lpr utilities video_surveillance")]
    [InlineData(false, "", "access_control administration events lpr utilities video_surveillance")]
    public async Task Authorize_ScopeDefaultsUseConfiguredThenSupportedQueryThenAll(
        bool configuredDefaults, string requestedScope, string expectedScope)
    {
        await using var fixture = await Fixture.Start(scopes: configuredDefaults
            ? [ToolScope.AccessControl, ToolScope.VideoSurveillance] : []);
        using var response = await fixture.Client.GetAsync(
            $"/authorize?response_type=code&redirect_uri=http://localhost/cb&code_challenge={Challenge}&code_challenge_method=S256&scope={Uri.EscapeDataString(requestedScope)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync();
        var checkboxes = Regex.Matches(page, "<input[^>]*class=\"scope-checkbox\"[^>]*value=\"([^\"]+)\"[^>]*>");
        var selected = checkboxes.Where(checkbox => checkbox.Value.Contains(" checked"))
            .Select(checkbox => checkbox.Groups[1].Value).Order().ToArray();
        Assert.Equal(expectedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(), selected);
        Assert.All(checkboxes, checkbox => Assert.DoesNotContain("disabled", checkbox.Value));
    }

    [Theory]
    [InlineData("utilities")]
    [InlineData("events utilities")]
    [InlineData("")]
    public async Task Authorize_ValidationErrorPreservesSubmittedScopeSelection(string submittedScope)
    {
        await using var fixture = await Fixture.Start(hosts: ["allowed.example.com"],
            scopes: [ToolScope.AccessControl, ToolScope.VideoSurveillance]);
        using var login = await fixture.Client.GetAsync($"/authorize?response_type=code&redirect_uri=http://localhost/cb&code_challenge={Challenge}&code_challenge_method=S256");
        var csrfToken = Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"csrf_token\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(csrfToken);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "user", ["password"] = "test",
            ["host"] = string.IsNullOrEmpty(submittedScope) ? "allowed.example.com" : "not-allowed.example.com",
            ["redirect_uri"] = "http://localhost/cb",
            ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256",
            ["scope"] = submittedScope, ["csrf_token"] = csrfToken
        });
        using var response = await fixture.Client.PostAsync("/authorize", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains(string.IsNullOrEmpty(submittedScope) ? "Please select at least one scope" : "not allowed", page);
        var selected = Regex.Matches(page, "<input[^>]*class=\"scope-checkbox\"[^>]*value=\"([^\"]+)\" checked[^>]*>")
            .Select(checkbox => checkbox.Groups[1].Value).Order().ToArray();
        Assert.Equal(submittedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(), selected);
    }

    [Theory]
    [InlineData("stdio", true, false)]
    [InlineData("http", false, true)]
    public void CliScopes_BindSharedDefaultsForEitherTransport(string transport, bool stdio, bool http)
    {
        var cli = CliOptions.Parse(["--transport", transport, "--scopes", "access_control,video_surveillance"]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Transport:Scopes:0"] = "Utilities" })
            .AddInMemoryCollection(cli.ToConfigOverrides()).Build();
        var options = configuration.GetSection(TransportOptions.SectionName).Get<TransportOptions>()!;

        Assert.Equal([ToolScope.AccessControl, ToolScope.VideoSurveillance], options.Scopes);
        Assert.Equal(stdio, options.Stdio.Enabled);
        Assert.Equal(http, options.Http.Enabled);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("unknown")]
    public void Pkce_NonS256MethodsAreRejected(string method)
    {
        Assert.False(TokenEndpoint.IsValidPkceChallenge(Verifier, method));
        Assert.False(TokenEndpoint.ValidatePkce(Verifier, Verifier, method));
    }

    [Fact]
    public void Pkce_S256RequiresValidMatchingVerifier()
    {
        Assert.True(TokenEndpoint.ValidatePkce(Verifier, Challenge, "S256"));
        Assert.False(TokenEndpoint.ValidatePkce(new string('a', 43), Challenge, "S256"));
        Assert.False(TokenEndpoint.ValidatePkce("short", Challenge, "S256"));
        Assert.False(TokenEndpoint.ValidatePkce(new string('a', 129), Challenge, "S256"));
        Assert.False(TokenEndpoint.ValidatePkce(new string('!', 43), Challenge, "S256"));
        Assert.False(TokenEndpoint.IsValidPkceChallenge("short", "S256"));
        Assert.False(TokenEndpoint.IsValidPkceChallenge(new string('!', 43), "S256"));
    }

    [Theory]
    [InlineData("client_secret_post", "missing", HttpStatusCode.Unauthorized)]
    [InlineData("client_secret_post", "wrong", HttpStatusCode.Unauthorized)]
    [InlineData("client_secret_post", "correct", HttpStatusCode.OK)]
    [InlineData("none", "missing", HttpStatusCode.OK)]
    public async Task CodeExchange_EnforcesRegisteredAuthenticationMethod(
        string method, string secretMode, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.Start();
        var registered = fixture.Registrations.Register(["https://example.com/cb"], ["authorization_code"], method, null);
        var code = fixture.Codes.Create("sc-user", registered.ClientId, "https://example.com/cb", Challenge, "S256", "utilities", "context");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = registered.ClientId,
            ["redirect_uri"] = code.RedirectUri, ["code"] = code.Code, ["code_verifier"] = Verifier
        };
        if (secretMode != "missing")
            form["client_secret"] = secretMode == "correct" ? registered.ClientSecret : "wrong";
        using var content = new FormUrlEncodedContent(form);
        using var response = await fixture.Client.PostAsync("/token", content);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("access_token", out _));
            Assert.Null(fixture.Codes.Redeem(code.Code));
        }
        else
            Assert.Same(code, fixture.Codes.Redeem(code.Code));
    }

    [Fact]
    public async Task ExistingDynamicClient_CannotUseClientCredentials()
    {
        await using var fixture = await Fixture.Start();
        var registered = fixture.Registrations.Register(["https://example.com/cb"], ["client_credentials"], "client_secret_post", null);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = registered.ClientId,
            ["client_secret"] = registered.ClientSecret
        });
        using var response = await fixture.Client.PostAsync("/token", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unsupported_grant_type", json.RootElement.GetProperty("error").GetString());
        Assert.False(json.RootElement.TryGetProperty("access_token", out _));
    }

    [Theory]
    [InlineData("client_secret_post", "missing", HttpStatusCode.Unauthorized)]
    [InlineData("client_secret_post", "wrong", HttpStatusCode.Unauthorized)]
    [InlineData("client_secret_post", "correct", HttpStatusCode.OK)]
    [InlineData("none", "missing", HttpStatusCode.OK)]
    public async Task Refresh_EnforcesTokenBoundClientAuthentication(
        string method, string secretMode, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.Start();
        var registered = fixture.Registrations.Register(["https://example.com/cb"], ["authorization_code"], method, null);
        var token = RefreshToken(registered.ClientId, client: registered);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = registered.ClientId, ["refresh_token"] = token
        };
        if (secretMode != "missing")
            form["client_secret"] = secretMode == "correct" ? registered.ClientSecret : "wrong";
        using var content = new FormUrlEncodedContent(form);
        using var response = await fixture.Client.PostAsync("/token", content);
        Assert.Equal(expected, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expected == HttpStatusCode.OK, json.RootElement.TryGetProperty("access_token", out _));
        Assert.Equal(expected == HttpStatusCode.OK, json.RootElement.TryGetProperty("refresh_token", out _));
        if (expected != HttpStatusCode.OK)
            Assert.Equal("invalid_client", json.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("different-client")]
    public async Task Refresh_RejectsMissingOrMismatchedClientId(string? requestedClientId)
    {
        await using var fixture = await Fixture.Start();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = RefreshToken("public-loopback")
        };
        if (requestedClientId is not null) form["client_id"] = requestedClientId;
        using var content = new FormUrlEncodedContent(form);
        using var response = await fixture.Client.PostAsync("/token", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(string.IsNullOrEmpty(requestedClientId) ? "invalid_request" : "invalid_grant",
            json.RootElement.GetProperty("error").GetString());
        Assert.False(json.RootElement.TryGetProperty("access_token", out _));
        Assert.False(json.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Theory]
    [InlineData("legacy-unbound")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-signature")]
    [InlineData("expired")]
    [InlineData("access-token")]
    [InlineData("wrong-algorithm")]
    [InlineData("unsigned")]
    [InlineData("missing-subject")]
    [InlineData("malformed")]
    public async Task Refresh_RejectsInvalidTokensWithoutIssuingTokens(string invalidCase)
    {
        await using var fixture = await Fixture.Start();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = "public-loopback",
            ["refresh_token"] = invalidCase == "malformed" ? "not-a-jwt" : RefreshToken("public-loopback", invalidCase)
        });
        using var response = await fixture.Client.PostAsync("/token", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", json.RootElement.GetProperty("error").GetString());
        Assert.False(json.RootElement.TryGetProperty("access_token", out _));
        Assert.False(json.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task Refresh_RejectsMissingClientAuthenticationInsteadOfDowngradingToPublic()
    {
        await using var fixture = await Fixture.Start();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = "dyn-removed-client",
            ["refresh_token"] = RefreshToken("dyn-removed-client", "missing-client-auth")
        });
        using var response = await fixture.Client.PostAsync("/token", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", json.RootElement.GetProperty("error").GetString());
        Assert.False(json.RootElement.TryGetProperty("access_token", out _));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("client_secret_post")]
    public async Task Refresh_SucceedsAfterRegistrationExpiresWhenTokenIsStillValid(string method)
    {
        var clock = new TestHelpers.TestClock();
        using var registrations = new ClientRegistrationStore(new MemoryCache(new MemoryCacheOptions { Clock = clock }), clock);
        var client = registrations.Register(["http://localhost/cb"], ["authorization_code"], method, null);
        await using var fixture = await Fixture.Start(registrations: registrations);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = client.ClientId,
            ["refresh_token"] = RefreshToken(client.ClientId, client: client)
        };
        if (method == "client_secret_post") form["client_secret"] = client.ClientSecret;
        using var initialContent = new FormUrlEncodedContent(form);
        using var initial = await fixture.Client.PostAsync("/token", initialContent);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(registrations.TryGet(client.ClientId, out _));
        using var expiredContent = new FormUrlEncodedContent(form);
        using var expired = await fixture.Client.PostAsync("/token", expiredContent);

        Assert.Equal(HttpStatusCode.OK, expired.StatusCode);
        using var json = JsonDocument.Parse(await expired.Content.ReadAsStringAsync());
        var renewedToken = json.RootElement.GetProperty("refresh_token").GetString()!;
        var renewed = new JwtSecurityTokenHandler().ReadJwtToken(renewedToken);
        Assert.Equal("sc-user", renewed.Subject);
        Assert.Equal(client.ClientId, renewed.Claims.Single(claim => claim.Type == "oauth_client_id").Value);
        Assert.Equal(method, renewed.Claims.Single(claim => claim.Type == "client_auth_method").Value);
        Assert.Equal("protected-context", renewed.Claims.Single(claim => claim.Type == "sc_ctx").Value);
        form["refresh_token"] = renewedToken;
        using var renewedContent = new FormUrlEncodedContent(form);
        using var repeated = await fixture.Client.PostAsync("/token", renewedContent);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    private static string RefreshToken(string oauthClientId, string? invalidCase = null, RegisteredClient? client = null)
    {
        var claims = new List<Claim>
        {
            new("token_type", invalidCase == "access-token" ? "Bearer" : "refresh"),
            new("scope", "utilities"), new("sc_ctx", "protected-context")
        };
        if (invalidCase != "missing-subject") claims.Add(new Claim(JwtRegisteredClaimNames.Sub, "sc-user"));
        if (invalidCase != "legacy-unbound") claims.Add(new Claim("oauth_client_id", oauthClientId));
        if (invalidCase != "missing-client-auth")
        {
            claims.Add(new Claim("client_auth_method", client?.TokenEndpointAuthMethod ?? "none"));
            if (client?.TokenEndpointAuthMethod == "client_secret_post")
                claims.Add(new Claim("client_secret_hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(client.ClientSecret)))));
        }
        var credentials = invalidCase == "unsigned" ? null : new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(invalidCase == "wrong-signature" ? new string('w', 64) : SigningKey)),
            invalidCase == "wrong-algorithm" ? SecurityAlgorithms.HmacSha512 : SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: invalidCase == "wrong-issuer" ? "other-issuer" : "GenetecSecurityCenter",
            audience: invalidCase == "wrong-audience" ? "other-audience" : "GenetecSecurityCenter",
            claims: claims, expires: DateTime.UtcNow.AddMinutes(invalidCase == "expired" ? -5 : 5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("unsupported")]
    public async Task Authorization_RejectsWeakPkceBeforeCredentialValidation(string method)
    {
        await using var fixture = await Fixture.Start();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "user", ["redirect_uri"] = "http://localhost/cb",
            ["code_challenge"] = Challenge, ["code_challenge_method"] = method
        });
        using var post = await fixture.Client.PostAsync("/authorize", form);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        using var get = await fixture.Client.GetAsync($"/authorize?response_type=code&redirect_uri=http://localhost/cb&code_challenge={Challenge}&code_challenge_method={method}");
        Assert.Equal(HttpStatusCode.BadRequest, get.StatusCode);
    }

    private sealed class Fixture(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public ClientRegistrationStore Registrations => app.Services.GetRequiredService<ClientRegistrationStore>();
        public AuthorizationCodeStore Codes => app.Services.GetRequiredService<AuthorizationCodeStore>();

        public static async Task<Fixture> Start(string[]? hosts = null, ToolScope[]? scopes = null,
            ClientRegistrationStore? registrations = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.Services.AddAntiforgery(options => options.FormFieldName = "csrf_token");
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.Configure<AuthenticationOptions>(options => options.SigningKey = SigningKey);
            builder.Services.Configure<SecurityCenterOptions>(options =>
                options.WebSdkSettings = new ConnectionOptions { Hosts = hosts ?? [] });
            builder.Services.Configure<TransportOptions>(options => options.Scopes = scopes ?? []);
            builder.Services.AddHttpClient(SecurityCenterSessionManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new ToolTestHandler());
            builder.Services.AddSingleton<SecurityCenterSessionManager>();
            if (registrations is null)
                builder.Services.AddSingleton<ClientRegistrationStore>();
            else
                builder.Services.AddSingleton(registrations);
            builder.Services.AddSingleton<AuthorizationCodeStore>();
            builder.Services.AddThrottling(builder.Configuration);
            builder.Services.AddAuthorizationThrottling();
            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapTokenEndpoint();
            app.MapAuthorizeEndpoint();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Fixture(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
