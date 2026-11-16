using System.Net;
using System.Reflection;
using System.Text;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Antiforgery;

namespace Genetec.Mcp.SecurityCenter.Authentication;

public static class AuthorizeEndpoint
{
    internal const string RateLimitPolicy = "authorize";
    private static readonly Assembly ResourceAssembly = typeof(AuthorizeEndpoint).Assembly;
    private static string? _loginTemplate;

    private static readonly (string Value, string DisplayName)[] KnownScopes =
    [
        ("video_surveillance", "Video Surveillance"),
        ("access_control", "Access Control"),
        ("lpr", "LPR"),
        ("events", "Events"),
        ("administration", "Administration"),
        ("utilities", "Utilities")
    ];

    public static WebApplication MapAuthorizeEndpoint(this WebApplication app)
    {
        app.MapGet("/authorize", (HttpContext context) =>
        {
            SetFrameProtection(context.Response);
            var query = context.Request.Query;
            var responseType = query["response_type"].ToString();
            var clientId = query["client_id"].ToString();
            var redirectUri = query["redirect_uri"].ToString();
            var state = query["state"].ToString();
            var codeChallenge = query["code_challenge"].ToString();
            var codeChallengeMethod = query["code_challenge_method"].ToString();
            var configuredScopes = context.RequestServices.GetService<IOptions<TransportOptions>>()?.Value.Scopes ?? [];
            var scope = configuredScopes.Length > 0
                ? string.Join(' ', configuredScopes.Select(scope =>
                    System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(scope.ToString())))
                : NormalizeScope(query["scope"].ToString());

            if (responseType != "code")
                return Results.BadRequest(new { error = "unsupported_response_type" });

            if (!OAuthRedirectUri.IsAllowed(redirectUri))
                return Results.BadRequest(new { error = "invalid_request" });

            if (!TokenEndpoint.IsValidPkceChallenge(codeChallenge, codeChallengeMethod))
                return Results.BadRequest(new { error = "invalid_request", error_description = "A valid S256 PKCE challenge is required." });

            var registrationStore = context.RequestServices.GetRequiredService<ClientRegistrationStore>();
            RegisteredClient? dynClient = null;
            var isDynamic = !string.IsNullOrEmpty(clientId) && registrationStore.TryGet(clientId, out dynClient);

            if (isDynamic && !dynClient!.RedirectUris.Contains(redirectUri))
                return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri not registered for this client" });

            if (!isDynamic && !OAuthRedirectUri.IsAllowed(redirectUri, requireLoopback: true))
                return Results.BadRequest(new { error = "invalid_request", error_description = "Non-loopback redirect_uri requires dynamic client registration" });

            var scOpts = context.RequestServices.GetService<IOptions<SecurityCenterOptions>>()?.Value;
            var configuredHost = GetSingleAllowedHostForDisplay(scOpts);
            var csrfToken = context.RequestServices.GetRequiredService<IAntiforgery>()
                .GetAndStoreTokens(context).RequestToken;
            return Results.Content(BuildLoginPage(
                clientId, redirectUri, state, codeChallenge, codeChallengeMethod, scope,
                host: configuredHost ?? "", hostReadOnly: configuredHost is not null,
                connectionOptions: scOpts?.WebSdkSettings, csrfToken: csrfToken), "text/html");
        }).AllowAnonymous();

        app.MapPost("/authorize", async (HttpContext context) =>
        {
            SetFrameProtection(context.Response);
            var form = await context.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var state = form["state"].ToString();
            var codeChallenge = form["code_challenge"].ToString();
            var codeChallengeMethod = form["code_challenge_method"].ToString();
            var clientId = form["client_id"].ToString();
            var scope = NormalizeScope(form["scope"].ToString());

            if (string.IsNullOrEmpty(username) || !OAuthRedirectUri.IsAllowed(redirectUri))
                return Results.BadRequest(new { error = "invalid_request" });

            if (!TokenEndpoint.IsValidPkceChallenge(codeChallenge, codeChallengeMethod))
                return Results.BadRequest(new { error = "invalid_request", error_description = "A valid S256 PKCE challenge is required." });

            var registrationStore = context.RequestServices.GetRequiredService<ClientRegistrationStore>();
            RegisteredClient? dynClient = null;
            var isDynamicClient = !string.IsNullOrEmpty(clientId) &&
                                  registrationStore.TryGet(clientId, out dynClient);

            if (isDynamicClient && !dynClient!.RedirectUris.Contains(redirectUri))
                return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri not registered for this client" });

            if (!isDynamicClient && !OAuthRedirectUri.IsAllowed(redirectUri, requireLoopback: true))
                return Results.BadRequest(new { error = "invalid_request", error_description = "Non-loopback redirect_uri requires dynamic client registration" });

            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_request", error_description = "Invalid sign-in token. Reload the sign-in page and try again." });

            var sessionManager = context.RequestServices.GetRequiredService<SecurityCenterSessionManager>();
            var scOpts = context.RequestServices.GetService<IOptions<SecurityCenterOptions>>()?.Value;
            var configuredHost = GetSingleAllowedHostForDisplay(scOpts);
            var hostRaw = form["host"].ToString().Trim();
            var (connection, error) = await ValidateLoginForm(form, scOpts, sessionManager);

            if (connection is null)
            {
                return Results.Content(BuildLoginPage(
                    clientId, redirectUri, state, codeChallenge, codeChallengeMethod, scope,
                    host: hostRaw, hostReadOnly: configuredHost is not null,
                    errorMessage: error, connectionOptions: scOpts?.WebSdkSettings,
                    selectAllWhenEmpty: false, csrfToken: antiforgery.GetAndStoreTokens(context).RequestToken), "text/html");
            }

            var store = context.RequestServices.GetRequiredService<AuthorizationCodeStore>();
            var protector = context.RequestServices.GetRequiredService<SessionContextProtector>();
            var oauthClientId = !string.IsNullOrEmpty(clientId) ? clientId : username;
            var scContext = protector.Protect(connection);
            var authCode = store.Create(username, oauthClientId, redirectUri, codeChallenge, codeChallengeMethod, scope, scContext);

            var separator = redirectUri.Contains('?') ? "&" : "?";
            var redirect = $"{redirectUri}{separator}code={Uri.EscapeDataString(authCode.Code)}";
            if (!string.IsNullOrEmpty(state))
                redirect += $"&state={Uri.EscapeDataString(state)}";

            return Results.Redirect(redirect);
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        return app.MapLoginImages();
    }

    internal static WebApplication MapLoginImages(this WebApplication app)
    {
        app.MapGet("/images/{fileName}", (string fileName) =>
        {
            var resourceName = $"images/{fileName}";
            var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return Results.NotFound();

            var contentType = fileName.EndsWith(".svg") ? "image/svg+xml"
                : fileName.EndsWith(".png") ? "image/png"
                : "application/octet-stream";

            return Results.Stream(stream, contentType);
        }).AllowAnonymous();

        return app;
    }

    internal static void SetFrameProtection(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        response.Headers.XFrameOptions = "DENY";
    }

    internal static string NormalizeScope(string raw)
    {
        var validValues = new HashSet<string>(KnownScopes.Select(s => s.Value));
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                       .Where(p => validValues.Contains(p));
        return string.Join(' ', parts);
    }

    internal static string BuildLoginPage(
        string clientId, string redirectUri, string state,
        string codeChallenge, string codeChallengeMethod,
        string scope, string host = "", bool hostReadOnly = false, string? errorMessage = null,
        ConnectionOptions? connectionOptions = null, bool showBrowserHttpWarning = true,
        bool selectAllWhenEmpty = true, string? csrfToken = null)
    {
        _loginTemplate ??= LoadTemplate();

        var errorHtml = errorMessage is not null
            ? $"""<div class="error-message">{WebUtility.HtmlEncode(errorMessage)}</div>"""
            : "";

        var preSelected = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var scopesHtml = BuildScopeCheckboxes(preSelected, selectAllWhenEmpty);

        var connectionWarnings = new StringBuilder();
        if (connectionOptions?.UseHttps == false)
            connectionWarnings.Append($"""
                <div class="security-notice connection-warning" role="alert">
                    <h3>{WebUtility.HtmlEncode(SecurityNotices.WebSdkHttpTitle)}</h3>
                    <p>{WebUtility.HtmlEncode(SecurityNotices.WebSdkHttpMessage)}</p>
                </div>
                """);
        if (connectionOptions is { UseHttps: true, SkipCertificateValidation: true })
            connectionWarnings.Append($"""
                <div class="security-notice connection-warning" role="alert">
                    <h3>{WebUtility.HtmlEncode(SecurityNotices.CertificateValidationTitle)}</h3>
                    <p>{WebUtility.HtmlEncode(SecurityNotices.CertificateValidationMessage)}</p>
                </div>
                """);

        return _loginTemplate
            .Replace("{{AGENT_ACCESS_TITLE}}", WebUtility.HtmlEncode(SecurityNotices.AgentAccessTitle))
            .Replace("{{AGENT_ACCESS_MESSAGE}}", WebUtility.HtmlEncode(SecurityNotices.AgentAccessMessage))
            .Replace("{{ERROR}}", errorHtml)
            .Replace("{{CSRF_TOKEN}}", csrfToken is null ? "" :
                $"""<input type="hidden" name="csrf_token" value="{WebUtility.HtmlEncode(csrfToken)}" />""")
            .Replace("{{CONNECTION_WARNINGS}}", connectionWarnings.ToString())
            .Replace("{{BROWSER_HTTP_WARNING}}", showBrowserHttpWarning ? $"""
                <div id="browserHttpWarning" class="security-notice connection-warning" role="alert" hidden>
                    <h3>{WebUtility.HtmlEncode(SecurityNotices.BrowserHttpTitle)}</h3>
                    <p>{WebUtility.HtmlEncode(SecurityNotices.BrowserHttpMessage)}</p>
                </div>
                """ : "")
            .Replace("{{HOST}}", WebUtility.HtmlEncode(host))
            .Replace("{{HOST_READONLY}}", hostReadOnly ? " readonly" : "")
            .Replace("{{REDIRECT_URI}}", WebUtility.HtmlEncode(redirectUri))
            .Replace("{{STATE}}", WebUtility.HtmlEncode(state))
            .Replace("{{CODE_CHALLENGE}}", WebUtility.HtmlEncode(codeChallenge))
            .Replace("{{CODE_CHALLENGE_METHOD}}", WebUtility.HtmlEncode(codeChallengeMethod))
            .Replace("{{CLIENT_ID}}", WebUtility.HtmlEncode(clientId))
            .Replace("{{SCOPES}}", scopesHtml);
    }

    internal static async Task<(ScConnectionContext? Connection, string? Error)> ValidateLoginForm(
        IFormCollection form, SecurityCenterOptions? options, SecurityCenterSessionManager sessionManager)
    {
        var host = form["host"].ToString().Trim();
        if (string.IsNullOrEmpty(host))
            return (null, "Please enter the Security Center host.");
        var (parsedHost, parsedPort) = ParseHostPort(host);
        if (!IsAllowedHost(parsedHost, options))
            return (null, "This Security Center host is not allowed.");
        if (string.IsNullOrEmpty(form["username"]))
            return (null, "Please enter your username.");
        if (string.IsNullOrEmpty(NormalizeScope(form["scope"].ToString())))
            return (null, "Please select at least one scope to continue.");

        var port = parsedPort ?? options?.WebSdkSettings?.Port ?? 4590;
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var result = await sessionManager.ValidateConnection(username, password, parsedHost, port);
        return result.Success
            ? (sessionManager.CreateConnectionContext(username, password, parsedHost, port), null)
            : (null, result.Error);
    }

    internal static string? GetSingleAllowedHostForDisplay(SecurityCenterOptions? options)
    {
        var hosts = options?.WebSdkSettings?.Hosts?
            .Where(host => !string.IsNullOrWhiteSpace(host)).Take(2).ToArray();
        return hosts is { Length: 1 } ? hosts[0].Trim() : null;
    }

    internal static bool IsAllowedHost(string host, SecurityCenterOptions? options) =>
        !string.IsNullOrWhiteSpace(host) &&
        options?.WebSdkSettings?.Hosts?.Any(allowed =>
            !string.IsNullOrWhiteSpace(allowed) &&
            string.Equals(host, NormalizeHost(allowed), StringComparison.OrdinalIgnoreCase)) == true;

    private static string NormalizeHost(string host)
    {
        host = host.Trim();
        return host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
    }

    internal static (string Host, int? Port) ParseHostPort(string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return ("", null);

        // IPv6 with port: [::1]:4590
        if (input.StartsWith('['))
        {
            var bracket = input.IndexOf(']');
            if (bracket >= 0)
            {
                var ipv6Host = input[1..bracket];
                var rest = input[(bracket + 1)..];
                if (rest.Length == 0)
                    return (ipv6Host, null);
                if (rest.StartsWith(':') && int.TryParse(rest[1..], out var p6) && p6 is >= 1 and <= 65535)
                    return (ipv6Host, p6);
                return (input, null);
            }
        }

        // IPv4 or hostname: split on last colon (bare IPv6 addresses have multiple colons so skip)
        var colon = input.LastIndexOf(':');
        if (colon > 0 && !input[..colon].Contains(':') && int.TryParse(input[(colon + 1)..], out var port) && port is >= 1 and <= 65535)
            return (input[..colon], port);

        return (input, null);
    }

    private static string BuildScopeCheckboxes(HashSet<string> preSelected, bool selectAllWhenEmpty)
    {
        var sb = new StringBuilder();
        var allSelected = selectAllWhenEmpty && preSelected.Count == 0;

        foreach (var (value, displayName) in KnownScopes)
        {
            var checkedAttr = (allSelected || preSelected.Contains(value)) ? " checked" : "";
            sb.Append($"""
                <label class="scope-item">
                    <input type="checkbox" class="scope-checkbox" value="{WebUtility.HtmlEncode(value)}"{checkedAttr} />
                    <span>{WebUtility.HtmlEncode(displayName)}</span>
                </label>
                """);
        }

        return sb.ToString();
    }

    private static string LoadTemplate()
    {
        using var stream = ResourceAssembly.GetManifestResourceStream("login.html")
            ?? throw new FileNotFoundException("Embedded login template not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
