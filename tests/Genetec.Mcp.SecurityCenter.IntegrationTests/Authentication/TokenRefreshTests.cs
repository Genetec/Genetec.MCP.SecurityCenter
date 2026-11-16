using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Audit;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class TokenRefreshTests
{
    private const string SigningKey = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private static string Challenge => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public async Task OAuth_RejectsUnsafeCallbacksBeforeAuthorization()
    {
        await using var fixture = await Fixture.Start();
        foreach (var redirectUri in new[]
        {
            "http://attacker.example/callback", "http://localhost.attacker.example/callback",
            "https://user:password@attacker.example/callback", "https://attacker.example/callback#fragment",
            "ftp://localhost/callback"
        })
        {
            using var registered = await fixture.Client.PostAsJsonAsync("/register", new
            {
                redirect_uris = new[] { redirectUri }
            });
            Assert.Equal(HttpStatusCode.BadRequest, registered.StatusCode);
            using var error = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
            Assert.Equal("invalid_client_metadata", error.RootElement.GetProperty("error").GetString());

            // Authorization must also reject unsafe callbacks already present in the store.
            var clientId = fixture.Registrations.Register([redirectUri], ["authorization_code"], "none", null).ClientId;
            using var get = await fixture.Client.GetAsync($"/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&code_challenge={Challenge}&code_challenge_method=S256&scope=utilities");
            Assert.Equal(HttpStatusCode.BadRequest, get.StatusCode);
            Assert.Null(get.Headers.Location);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "test-user", ["client_id"] = clientId, ["redirect_uri"] = redirectUri,
                ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256", ["scope"] = "utilities"
            });
            using var post = await fixture.Client.PostAsync("/authorize", form);
            Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
            Assert.Null(post.Headers.Location);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown_scope")]
    public async Task Mcp_NoRecognizedTokenScopes_HidesAndRejectsScopedTools(string? scope)
    {
        var handler = new SignInFailureHandler("connection");
        await using var fixture = await Fixture.Start(handler);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "scope-test-user") };
        if (scope is not null) claims.Add(new Claim("scope", scope));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "GenetecSecurityCenter", audience: "GenetecSecurityCenter", claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        var sessionId = await fixture.InitializeMcp(token);

        using var listed = await fixture.McpPost(token,
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""", sessionId);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listJson = await Fixture.ReadMcpResult(listed);
        var tools = listJson.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal("authentication_probe", Assert.Single(tools).GetProperty("name").GetString());

        foreach (var toolName in new[] { "user_delete", "cardholder_delete" })
        {
            using var called = await fixture.McpPost(token,
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = toolName, arguments = new { } } }), sessionId);
            Assert.Equal(HttpStatusCode.OK, called.StatusCode);
            using var callJson = await Fixture.ReadMcpResult(called);
            var result = callJson.RootElement.GetProperty("result");
            Assert.True(result.GetProperty("isError").GetBoolean());
            Assert.Equal("Access denied: token contains no recognized tool scopes.",
                result.GetProperty("content")[0].GetProperty("text").GetString());
        }
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task StdioAuthorization_RequiresValidToken()
    {
        var handler = new SignInFailureHandler("connection");
        await using var fixture = await Fixture.Start(handler);
        await using var signIn = fixture.CreateStdioSignIn();
        var pending = await signIn.SignInAsync();

        foreach (var token in new[] { "", new string('0', 64) })
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "test-user", ["password"] = "browser-password",
                ["host"] = "allowed.example", ["scope"] = "utilities", ["csrf_token"] = token
            });
            using var response = await fixture.Client.PostAsync(pending.SignInUrl, form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, handler.RequestCount);
            Assert.False(fixture.Manager.HasSession());
        }
    }

    private static string ReadCsrfToken(string page)
    {
        var token = Regex.Match(page, "name=\"csrf_token\" value=\"([^\"]+)\"");
        Assert.True(token.Success, "The sign-in form must contain a CSRF token.");
        return token.Groups[1].Value;
    }

    private static async Task<string> GetCsrfToken(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return ReadCsrfToken(await response.Content.ReadAsStringAsync());
    }

    private static string HttpAuthorizeUrl => $"/authorize?response_type=code&client_id=public-loopback&redirect_uri=http://localhost/cb&code_challenge={Challenge}&code_challenge_method=S256&scope=utilities";

    [Fact]
    public async Task HttpAuthorization_RequiresTokenBoundToBrowserCookie()
    {
        var handler = new SignInFailureHandler("credentials");
        await using var fixture = await Fixture.Start(handler);
        var token = await GetCsrfToken(fixture.Client, HttpAuthorizeUrl);
        using var attacker = new HttpClient { BaseAddress = fixture.Client.BaseAddress };
        foreach (var (client, submittedToken) in new[]
        {
            (fixture.Client, ""), (fixture.Client, "invalid-token"), (attacker, token), (fixture.Client, token)
        })
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "public-loopback", ["redirect_uri"] = "http://localhost/cb",
                ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256", ["scope"] = "utilities",
                ["username"] = "test-user", ["password"] = "browser-password", ["host"] = "allowed.example",
                ["csrf_token"] = submittedToken
            });
            using var response = await client.PostAsync("/authorize", form);
            var valid = client == fixture.Client && submittedToken == token;
            Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Equal(valid ? 1 : 0, handler.RequestCount);
            Assert.False(fixture.Manager.HasSession());
            if (valid)
            {
                var page = await response.Content.ReadAsStringAsync();
                Assert.Contains("Invalid Security Center credentials", page);
                Assert.NotEmpty(ReadCsrfToken(page));
            }
        }
    }

    [Theory]
    [InlineData(true, true, "SSL/TLS handshake failed")]
    [InlineData(true, false, "secure connection")]
    [InlineData(false, true, "HTTP and")]
    public async Task ValidateConnection_RealTlsFailuresExplainCertificateAndProtocolSettings(bool useHttps, bool serverHttps, string expected)
    {
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        certificateRequest.CertificateExtensions.Add(names.Build());
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, endpoint =>
        {
            if (serverHttps) endpoint.UseHttps(certificate);
        }));
        await using var webSdk = builder.Build();
        webSdk.MapGet("/WebSdk/", () => Results.Json(new { Rsp = new { Status = "Ok" } }));
        await webSdk.StartAsync();
        var address = new Uri(webSdk.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single());
        var manager = ScIntegrationFixture.BuildManager(settings: new ConnectionOptions
        {
            UseHttps = useHttps, Port = address.Port, InvokeTimeoutSec = 5
        });

        var (success, error) = await manager.ValidateConnection("test-user", "test-password", "localhost");

        Assert.False(success);
        Assert.Contains(expected, error);
        Assert.Contains($"port is configured for {(useHttps ? "HTTPS" : "HTTP")} and", error);
        Assert.DoesNotContain("Invalid Security Center credentials", error);
        Assert.False(manager.HasSession());
        await webSdk.StopAsync();
    }

    [Theory]
    [InlineData(false, "credentials", "Invalid Security Center credentials")]
    [InlineData(true, "credentials", "Invalid Security Center credentials")]
    [InlineData(false, "connection", "Could not reach")]
    [InlineData(true, "connection", "Could not reach")]
    [InlineData(false, "tls", "SSL/TLS handshake failed")]
    [InlineData(true, "tls", "SSL/TLS handshake failed")]
    [InlineData(false, "dns", "hostname could not be resolved")]
    [InlineData(true, "dns", "hostname could not be resolved")]
    [InlineData(false, "timeout", "did not respond in time")]
    [InlineData(true, "timeout", "did not respond in time")]
    [InlineData(false, "server", "server error (HTTP 503)")]
    [InlineData(true, "server", "server error (HTTP 503)")]
    [InlineData(false, "response", "unexpected response")]
    [InlineData(true, "response", "unexpected response")]
    [InlineData(false, "websdk", "could not complete the sign-in request")]
    [InlineData(true, "websdk", "could not complete the sign-in request")]
    public async Task AuthorizationHtml_ShowsConnectionFailureForHttpAndStdio(bool stdio, string failure, string expected)
    {
        await using var fixture = await Fixture.Start(new SignInFailureHandler(failure));
        await using var signIn = fixture.CreateStdioSignIn();
        using var services = new ServiceCollection().AddSingleton(signIn).BuildServiceProvider();
        var url = "/authorize";
        if (stdio)
        {
            var result = await SessionManagementTool.Execute(fixture.Manager, services,
                new SessionManagementRequest(SessionAction.Connect));
            url = Assert.IsType<StdioSignInResult>(result).SignInUrl!;
        }
        var fields = new Dictionary<string, string>
        {
            ["client_id"] = "public-loopback", ["username"] = "test-user", ["password"] = "browser-password",
            ["host"] = "allowed.example", ["redirect_uri"] = "http://localhost/cb",
            ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256", ["scope"] = "utilities"
        };
        fields["csrf_token"] = await GetCsrfToken(fixture.Client, stdio ? url : HttpAuthorizeUrl);
        using var form = new FormUrlEncodedContent(fields);

        using var response = await fixture.Client.PostAsync(url, form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, page);
        Assert.DoesNotContain("sensitive-server-details", page);
        Assert.DoesNotContain("browser-password", page);
        if (failure != "credentials") Assert.DoesNotContain("Invalid Security Center credentials", page);
        Assert.False(fixture.Manager.HasSession());
        if (stdio) Assert.NotNull(signIn.PendingSignIn);
    }

    private sealed class SignInFailureHandler(string failure) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            const string details = "sensitive-server-details";
            var response = failure switch
            {
                "credentials" => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(details) },
                "server" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(details) },
                "response" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<html>{details}</html>") },
                "websdk" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"Rsp\":{{\"Status\":\"Fail\",\"Result\":\"{details}\"}}}}") },
                "connection" => throw new HttpRequestException(HttpRequestError.ConnectionError, details),
                "tls" => throw new HttpRequestException(HttpRequestError.SecureConnectionError, details),
                "dns" => throw new HttpRequestException(HttpRequestError.NameResolutionError, details),
                "timeout" => throw new TaskCanceledException(details, new TimeoutException()),
                _ => throw new ArgumentException("Unknown test failure", nameof(failure))
            };
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task HttpCorrelationId_PreservesBoundedIdOrReturnsServerId(int length, bool accepted)
    {
        await using var fixture = await Fixture.Start();
        var incomingId = new string('a', length);
        fixture.Client.DefaultRequestHeaders.Add("X-Correlation-Id", incomingId);

        using var response = await fixture.Client.GetAsync("/authorize");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var returnedId = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        if (accepted)
            Assert.Equal(incomingId, returnedId);
        else
        {
            Assert.False(string.IsNullOrEmpty(returnedId));
            Assert.NotEqual(incomingId, returnedId);
        }
    }

    [Theory]
    [InlineData("client_secret_post")]
    [InlineData("none")]
    [InlineData("static")]
    public async Task CodeExchangeAndRefresh_PreserveSecurityCenterIdentityContextAndClientBinding(string method)
    {
        await using var fixture = await Fixture.Start();
        var registration = method == "static" ? null : fixture.Registrations.Register(
            ["http://localhost/cb"], ["authorization_code"], method, null);
        var clientId = registration?.ClientId ?? "public-loopback";
        var secret = method == "client_secret_post" ? registration!.ClientSecret : null;
        var connection = new ScConnectionContext { Host = "sc.test", User = "sc-user", Password = "test-password" };
        var protectedContext = fixture.Protector.Protect(connection);
        var code = fixture.Codes.Create(connection.User, clientId, "http://localhost/cb", Challenge,
            "S256", "utilities events", protectedContext);
        var exchange = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = clientId, ["code"] = code.Code,
            ["redirect_uri"] = "http://localhost/cb", ["code_verifier"] = Verifier
        };
        if (secret is not null) exchange["client_secret"] = secret;
        using var initial = await fixture.Post(exchange);
        AssertTokenState(initial.RootElement, fixture, clientId, connection, protectedContext);
        await fixture.AssertMcpAcceptsOnlyAccessToken(initial.RootElement);

        var refresh = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = clientId,
            ["refresh_token"] = initial.RootElement.GetProperty("refresh_token").GetString()!
        };
        if (secret is not null) refresh["client_secret"] = secret;
        using var renewed = await fixture.Post(refresh);
        AssertTokenState(renewed.RootElement, fixture, clientId, connection, protectedContext);
        await fixture.AssertMcpAcceptsOnlyAccessToken(renewed.RootElement);
        Assert.NotEqual(initial.RootElement.GetProperty("refresh_token").GetString(),
            renewed.RootElement.GetProperty("refresh_token").GetString());
    }

    [Theory]
    [InlineData("login")]
    [InlineData("invalid_host")]
    [InlineData("missing_scope")]
    public async Task AuthorizationHtml_PreventsFramingIncludingValidationErrors(string scenario)
    {
        await using var fixture = await Fixture.Start();
        var csrfToken = await GetCsrfToken(fixture.Client, HttpAuthorizeUrl);
        using var response = scenario == "login"
            ? await fixture.Client.GetAsync($"/authorize?response_type=code&client_id=public-loopback&redirect_uri=http://localhost/cb&code_challenge={Challenge}&code_challenge_method=S256")
            : await fixture.Client.PostAsync("/authorize", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "public-loopback", ["username"] = "test-user", ["host"] = scenario == "invalid_host" ? "not-allowed.example" : "allowed.example",
                ["redirect_uri"] = "http://localhost/cb", ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256",
                ["scope"] = scenario == "missing_scope" ? "" : "utilities", ["csrf_token"] = csrfToken
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", page);
        if (scenario != "login")
            Assert.Contains(scenario == "invalid_host" ? "not allowed" : "Please select at least one scope", page);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StdioAuthorizationHtml_PreventsFramingIncludingValidationErrors(bool validationError)
    {
        await using var fixture = await Fixture.Start();
        await using var signIn = fixture.CreateStdioSignIn();
        var pending = await signIn.SignInAsync();
        Assert.Equal("Pending", pending.Status);
        using var client = new HttpClient();
        var token = await GetCsrfToken(client, pending.SignInUrl!);
        using var response = validationError
            ? await client.PostAsync(pending.SignInUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "test-user", ["host"] = "not-allowed.example", ["scope"] = "utilities", ["csrf_token"] = token
            }))
            : await client.GetAsync(pending.SignInUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", page);
        if (validationError) Assert.Contains("not allowed", page);
    }

    [Fact]
    public async Task RestartWithSameSigningKey_RefreshesWithoutDynamicRegistration()
    {
        string clientId, secret, refreshToken;
        await using (var original = await Fixture.Start())
        {
            var registration = original.Registrations.Register(["http://localhost/cb"],
                ["authorization_code"], "client_secret_post", null);
            clientId = registration.ClientId;
            secret = registration.ClientSecret;
            var code = original.Codes.Create("sc-user", clientId, "http://localhost/cb", Challenge,
                "S256", "utilities", "");
            using var tokens = await original.Post(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["client_id"] = clientId, ["client_secret"] = secret,
                ["code"] = code.Code, ["redirect_uri"] = "http://localhost/cb", ["code_verifier"] = Verifier
            });
            refreshToken = tokens.RootElement.GetProperty("refresh_token").GetString()!;
        }
        await using var restarted = await Fixture.Start();
        Assert.False(restarted.Registrations.TryGet(clientId, out _));
        Assert.Equal(clientId, restarted.Validate(refreshToken).FindFirstValue("oauth_client_id"));
        using var renewed = await restarted.Post(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["client_secret"] = secret,
            ["refresh_token"] = refreshToken
        });
        var principal = restarted.Validate(renewed.RootElement.GetProperty("refresh_token").GetString()!);
        Assert.Equal("sc-user", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal(clientId, principal.FindFirstValue("oauth_client_id"));
        Assert.Equal("client_secret_post", principal.FindFirstValue("client_auth_method"));
        Assert.Equal("utilities", principal.FindFirstValue("scope"));
        await restarted.AssertMcpAcceptsOnlyAccessToken(renewed.RootElement);
    }

    private static void AssertTokenState(JsonElement tokens, Fixture fixture, string clientId,
        ScConnectionContext connection, string protectedContext)
    {
        Assert.Equal("utilities events", tokens.GetProperty("scope").GetString());
        foreach (var name in new[] { "access_token", "refresh_token" })
        {
            var principal = fixture.Validate(tokens.GetProperty(name).GetString()!);
            Assert.Equal(connection.User, principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
            Assert.Equal("utilities events", principal.FindFirstValue("scope"));
            Assert.Equal(protectedContext, principal.FindFirstValue("sc_ctx"));
            var recovered = fixture.Protector.Unprotect(principal.FindFirstValue("sc_ctx")!);
            Assert.NotNull(recovered);
            Assert.Equal(connection.Host, recovered.Host);
            Assert.Equal(connection.User, recovered.User);
            Assert.Equal(connection.Password, recovered.Password);
            if (name == "refresh_token")
            {
                Assert.Equal("refresh", principal.FindFirstValue("token_type"));
                Assert.Equal(clientId, principal.FindFirstValue("oauth_client_id"));
            }
        }
    }

    private sealed class Fixture(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public SecurityCenterSessionManager Manager => app.Services.GetRequiredService<SecurityCenterSessionManager>();
        public ClientRegistrationStore Registrations => app.Services.GetRequiredService<ClientRegistrationStore>();
        public AuthorizationCodeStore Codes => app.Services.GetRequiredService<AuthorizationCodeStore>();
        public SessionContextProtector Protector => app.Services.GetRequiredService<SessionContextProtector>();

        public StdioSignInService CreateStdioSignIn() => new(
            app.Services.GetRequiredService<SecurityCenterSessionManager>(),
            app.Services.GetRequiredService<IOptions<SecurityCenterOptions>>(),
            app.Configuration) { BrowserLauncher = _ => { } };

        public static async Task<Fixture> Start(HttpMessageHandler? webSdkHandler = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSecurityCenterAuthentication(builder.Configuration, SigningKey);
            builder.Services.AddHttpContextAccessor();
            builder.Services.Configure<SecurityCenterOptions>(options =>
                options.WebSdkSettings = new ConnectionOptions { Hosts = ["allowed.example"] });
            var webSdkClient = builder.Services.AddHttpClient(SecurityCenterSessionManager.HttpClientName);
            if (webSdkHandler is not null)
                webSdkClient.ConfigurePrimaryHttpMessageHandler(() => webSdkHandler);
            builder.Services.AddSingleton<SecurityCenterSessionManager>();
            builder.Services.AddMcpServer().WithHttpTransport(options =>
                options.SessionMode = ModelContextProtocol.AspNetCore.HttpServerSessionMode.StatefulForInitializeClients)
                .WithTools<AuthenticationProbe>()
                .WithTools<Genetec.Mcp.SecurityCenter.Tools.Administration.UserDeleteTool>()
                .WithTools<Genetec.Mcp.SecurityCenter.Tools.AccessControl.CardholderDeleteTool>()
                .WithRequestFilters(filters => filters.AddScopedToolFilter());
            builder.Services.AddThrottling(builder.Configuration);
            builder.Services.AddAuthorizationThrottling();
            var app = builder.Build();
            app.UseRouting();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapTokenEndpoint();
            app.MapClientRegistrationEndpoint();
            app.MapAuthorizeEndpoint();
            app.MapMcp("/mcp").RequireAuthorization();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Fixture(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task AssertMcpAcceptsOnlyAccessToken(JsonElement tokens)
        {
            var accessToken = tokens.GetProperty("access_token").GetString()!;
            var refreshToken = tokens.GetProperty("refresh_token").GetString()!;
            var sessionId = await InitializeMcp(accessToken);
            const string call = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"authentication_probe\",\"arguments\":{}}}";
            using var accepted = await McpPost(accessToken, call, sessionId);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var callJson = await ReadMcpResult(accepted);
            Assert.Equal("authenticated", callJson.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());

            using var rejectedInitialize = await McpPost(refreshToken, InitializeRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, rejectedInitialize.StatusCode);
            using var rejectedCall = await McpPost(refreshToken, call, sessionId);
            Assert.Equal(HttpStatusCode.Unauthorized, rejectedCall.StatusCode);
            Assert.Contains(rejectedCall.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        }

        private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"authentication-test\",\"version\":\"1\"}}}";

        public async Task<string> InitializeMcp(string token)
        {
            using var initialized = await McpPost(token, InitializeRequest);
            Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
            using var initializeJson = await ReadMcpResult(initialized);
            Assert.Equal("2025-06-18", initializeJson.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
            return Assert.Single(initialized.Headers.GetValues("Mcp-Session-Id"));
        }

        public async Task<HttpResponseMessage> McpPost(string token, string body, string? sessionId = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (sessionId is not null)
            {
                request.Headers.Add("Mcp-Session-Id", sessionId);
                request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            }
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return await client.SendAsync(request);
        }

        public static async Task<JsonDocument> ReadMcpResult(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            var json = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
                ? Assert.Single(body.Split('\n'), line => line.StartsWith("data:", StringComparison.Ordinal))[5..].Trim()
                : body;
            return JsonDocument.Parse(json);
        }

        public ClaimsPrincipal Validate(string token)
        {
            var options = app.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
            return new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = options.Issuer,
                    ValidateAudience = true, ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                }, out _);
        }

        public async Task<JsonDocument> Post(Dictionary<string, string> form, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync("/token", content);
            Assert.Equal(expected, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [McpServerToolType]
    public sealed class AuthenticationProbe
    {
        [McpServerTool(Name = "authentication_probe")]
        public static string Execute() => "authenticated";
    }
}
