using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Security.Claims;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Client;

public class SecurityCenterSessionManager
{
    public const string HttpClientName = "sc";

    private const string DefaultClientId = "__default__";
    private const string DefaultAlias = "default";
    private const string CacheKeyPrefix = "scm:";
    private static readonly TimeSpan DefaultSessionTtl = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, UserSessionState> _bootstrap = new();

    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ConnectionOptions? _defaultConnectionOptions;
    private readonly IMemoryCache? _cache;
    private readonly SessionContextProtector? _contextProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger? _webSdkLogger;
    private readonly TimeSpan _sessionTtl;
    private readonly SdkConcurrencyLimiter? _sdkLimiter;
    private string? _stdioClientId;

    // Replaced atomically after browser sign-in; readers never observe a partially changed set.
    public HashSet<ToolScope> StdioScopes { get; set; } = [];

    public SecurityCenterSessionManager(
        IOptions<SecurityCenterOptions> scOptions,
        IHttpClientFactory httpClientFactory,
        IServiceProvider services)
    {
        _defaultConnectionOptions = scOptions.Value.WebSdkSettings;
        _httpClientFactory = httpClientFactory;
        _sdkLimiter = services.GetService<SdkConcurrencyLimiter>();
        _webSdkLogger = services.GetService<ILoggerFactory>()?.CreateLogger(WebSdkLogging.CategoryName);
        _httpContextAccessor = services.GetService<IHttpContextAccessor>();
        _cache = services.GetService<IMemoryCache>();
        _contextProtector = services.GetService<SessionContextProtector>();
        var authOptions = services.GetService<IOptions<AuthenticationOptions>>();
        _sessionTtl = authOptions?.Value.TokenLifetimeMinutes is > 0 and var minutes
            ? TimeSpan.FromMinutes(minutes)
            : DefaultSessionTtl;
    }

    public string? ActiveAlias => IsHttpAuthenticatedRequest
        ? GetHttpSession() is not null ? DefaultAlias : null
        : GetCurrentState()?.ActiveAlias;

    public void Connect(string alias, ScConnectionContext context)
    {
        if (IsHttpAuthenticatedRequest)
            throw new InvalidOperationException("Connection changes are not supported in HTTP mode.");

        var session = new SecurityCenterSession(context);
        var state = GetOrCreateState(GetCurrentClientId());
        state.Aliases[alias] = session;
        state.ActiveAlias = alias;
    }

    public void SetActive(string alias)
    {
        var state = GetCurrentState();
        if (state is null || !state.Aliases.ContainsKey(alias))
            throw new InvalidOperationException($"No session with alias '{alias}'.");
        state.ActiveAlias = alias;
    }

    public void Disconnect(string alias)
    {
        var state = GetCurrentState();
        if (state is null) return;
        state.Aliases.TryRemove(alias, out _);
        if (state.ActiveAlias == alias)
            state.ActiveAlias = state.Aliases.Keys.LastOrDefault();
    }

    public void Logout()
    {
        if (IsHttpAuthenticatedRequest)
            return;

        var clientId = GetCurrentClientId();
        if (_cache is not null)
            _cache.Remove(CacheKeyPrefix + clientId);
        else
            _bootstrap.TryRemove(clientId, out _);
        if (_stdioClientId == clientId)
            _stdioClientId = null;
    }

    public List<string> ListSessions() => IsHttpAuthenticatedRequest
        ? GetHttpSession() is not null ? [DefaultAlias] : []
        : GetCurrentState()?.Aliases.Keys.ToList() ?? [];

    public bool HasSession()
    {
        if (IsHttpAuthenticatedRequest)
            return GetHttpSession() is not null;

        var state = GetCurrentState() ?? GetState(DefaultClientId);
        return state?.Aliases.Count > 0;
    }

    public SecurityCenterRestClient GetClient(CancellationToken cancellationToken = default)
    {
        if (IsHttpAuthenticatedRequest)
        {
            var httpSession = GetHttpSession();
            if (httpSession is null)
                throw new InvalidOperationException(
                    "No valid Security Center connection context was found in the access token.");

            return new SecurityCenterRestClient(httpSession, CreateHttpClient(), _webSdkLogger, cancellationToken, _sdkLimiter, CurrentClientId);
        }

        var state = GetCurrentState();

        SecurityCenterSession? session = null;

        if (state is not null)
        {
            if (state.ActiveAlias is not null)
                state.Aliases.TryGetValue(state.ActiveAlias, out session);
            if (session is null && state.Aliases.Count > 0)
                session = state.Aliases.Values.First();
        }

        if (session is null)
            throw new InvalidOperationException(
                "No active Security Center session. Call session_management with action 'connect' to sign in.");

        return new SecurityCenterRestClient(session, CreateHttpClient(), _webSdkLogger, cancellationToken, _sdkLimiter, CurrentClientId);
    }

    public async Task<(bool Success, string? Error)> ValidateAndConnect(string scUser, string scPassword, string scHost, int? scPort = null)
    {
        var context = CreateConnectionContext(scUser, scPassword, scHost, scPort);
        var validationResult = await ValidateConnection(context);
        if (!validationResult.Success)
            return validationResult;

        var session = new SecurityCenterSession(context);
        var sessionKey = $"{context.User}@{context.Host}:{context.Port}";
        var state = GetOrCreateState(sessionKey);
        state.Aliases[DefaultAlias] = session;
        state.ActiveAlias = DefaultAlias;
        _stdioClientId = sessionKey;
        return (true, null);
    }

    public Task<(bool Success, string? Error)> ValidateConnection(
        string scUser, string scPassword, string scHost, int? scPort = null) =>
        ValidateConnection(CreateConnectionContext(scUser, scPassword, scHost, scPort));

    private async Task<(bool Success, string? Error)> ValidateConnection(ScConnectionContext context)
    {
        var session = new SecurityCenterSession(context);
        var testClient = new SecurityCenterRestClient(session, CreateHttpClient(), _webSdkLogger);

        try
        {
            var result = await testClient.TestConnection();
            return SecurityCenterRestClient.TryGetErrorMessage(result, out _)
                ? (false, "Security Center Web SDK rejected the sign-in request. Ask your administrator to check the Web SDK configuration and server logs.")
                : (true, null);
        }
        catch (Exception ex)
        {
            return (false, GetConnectionError(ex, context.UseHttps));
        }
    }

    private static string GetConnectionError(Exception exception, bool useHttps)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is WebSdkCertificateValidationException certificateError)
                return certificateError.Message;
        }

        var protocolHint = $"Check that the Web SDK port is configured for {(useHttps ? "HTTPS" : "HTTP")} and that the server's UseHttps setting matches it.";

        // Do not expose exception messages or response bodies: they can contain credentials
        // or internal server details. HTTP status and transport errors are safe to classify.
        if (exception is HttpRequestException httpError)
        {
            if (httpError.StatusCode is { } status)
            {
                return status switch
                {
                    HttpStatusCode.Unauthorized => "Invalid Security Center credentials, or the configured application ID was rejected. Check your username and password; ask your administrator to verify the application ID.",
                    HttpStatusCode.Forbidden => $"Security Center Web SDK denied access (HTTP 403). Ask your administrator to check your Web SDK access permissions and whether HTTPS is required. {protocolHint}",
                    HttpStatusCode.NotFound => "Security Center Web SDK endpoint was not found (HTTP 404). Check the host and port and that the Web SDK role is enabled.",
                    HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "Security Center Web SDK did not respond in time. Check the connection and Web SDK service, then try again.",
                    HttpStatusCode.TooManyRequests => "Security Center Web SDK is receiving too many requests (HTTP 429). Wait a moment and try again.",
                    HttpStatusCode.BadRequest => $"Security Center Web SDK rejected the connection request (HTTP 400). {protocolHint}",
                    _ when (int)status >= 500 => $"Security Center Web SDK returned a server error (HTTP {(int)status}). Check the Web SDK service or ask your administrator to review its logs.",
                    _ => $"Security Center Web SDK rejected the connection request (HTTP {(int)status}). Ask your administrator to check the Web SDK configuration."
                };
            }

            return httpError.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "The Security Center Web SDK hostname could not be resolved. Check the hostname and DNS or VPN connection.",
                HttpRequestError.SecureConnectionError => $"Could not establish a secure connection to Security Center Web SDK: the SSL/TLS handshake failed. Check the Web SDK TLS settings. {protocolHint}",
                HttpRequestError.ConnectionError => $"Could not reach Security Center Web SDK. Check the host, port, network or VPN connection, firewall, and that the Web SDK role is running. {protocolHint}",
                HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded or HttpRequestError.HttpProtocolError => $"Security Center Web SDK returned an invalid or incomplete HTTP response. {protocolHint}",
                _ => $"Could not communicate with Security Center Web SDK. Check the connection and Web SDK service. {protocolHint}"
            };
        }

        return exception switch
        {
            OperationCanceledException => "Security Center Web SDK did not respond in time. Check the connection and Web SDK service, then try again.",
            JsonException => $"The server returned an unexpected response instead of Web SDK JSON. Check the host, port, and that the Web SDK role is enabled. {protocolHint}",
            InvalidOperationException => "Security Center Web SDK could not complete the sign-in request. Ask your administrator to check the Web SDK configuration and server logs.",
            _ => "Could not complete Security Center sign-in. Ask your administrator to check the server configuration and logs."
        };
    }

    public ScConnectionContext CreateConnectionContext(
        string scUser, string scPassword, string scHost, int? scPort = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scHost);
        return new ScConnectionContext
        {
            Host = scHost,
            Port = scPort ?? _defaultConnectionOptions?.Port ?? 4590,
            User = scUser,
            Password = scPassword ?? "",
            UseHttps = _defaultConnectionOptions?.UseHttps ?? true,
            SkipCertificateValidation = _defaultConnectionOptions?.SkipCertificateValidation ?? false,
            InvokeTimeoutSec = _defaultConnectionOptions?.InvokeTimeoutSec ?? 30
        };
    }

    private UserSessionState GetOrCreateState(string clientId)
    {
        if (_cache is not null)
            return _cache.GetOrCreate(CacheKeyPrefix + clientId, e =>
            {
                e.SetOptions(CreateCacheOptions());
                return new UserSessionState();
            })!;
        return _bootstrap.GetOrAdd(clientId, _ => new UserSessionState());
    }

    private UserSessionState? GetCurrentState() =>
        IsHttpAuthenticatedRequest ? null : GetState(GetCurrentClientId());

    private UserSessionState? GetState(string clientId)
    {
        if (_cache is not null)
            return _cache.TryGetValue(CacheKeyPrefix + clientId, out UserSessionState? s) ? s : null;
        return _bootstrap.GetValueOrDefault(clientId);
    }

    private HttpClient CreateHttpClient() =>
        _httpClientFactory.CreateClient(HttpClientName);

    private SecurityCenterSession? GetHttpSession()
    {
        if (!IsHttpAuthenticatedRequest || _contextProtector is null)
            return null;

        var context = _httpContextAccessor!.HttpContext!;
        var connection = _contextProtector.Unprotect(context.User.FindFirstValue("sc_ctx"));
        return connection is null ? null : new SecurityCenterSession(connection);
    }

    public string CurrentClientId => GetCurrentClientId();

    public void SetStdioClientId(string user, string scHost) =>
        _stdioClientId = $"{user}@{scHost}";

    private string GetCurrentClientId()
    {
        var context = _httpContextAccessor?.HttpContext;
        if (context?.User?.Identity?.IsAuthenticated == true)
        {
            var clientId = context.User.FindFirstValue("sub")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("client_id");
            if (clientId != null)
                return clientId;
        }
        return _stdioClientId ?? DefaultClientId;
    }

    private bool IsHttpAuthenticatedRequest =>
        _httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true;

    private MemoryCacheEntryOptions CreateCacheOptions() =>
        new() { SlidingExpiration = _sessionTtl };

    private sealed class UserSessionState
    {
        public ConcurrentDictionary<string, SecurityCenterSession> Aliases { get; } = new();
        public string? ActiveAlias { get; set; }
    }
}
