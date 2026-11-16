using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.Authentication;

/// <summary>Hosts the existing sign-in form on loopback until sign-in completes or expires.</summary>
public sealed class StdioSignInService(
    SecurityCenterSessionManager sessionManager,
    IOptions<SecurityCenterOptions> options,
    IConfiguration configuration) : IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan SignInLifetime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _signInLock = new(1, 1);
    private Task _signInTask = Task.CompletedTask;
    private CancellationTokenSource? _timeout;
    private StdioSignInResult? _pendingSignIn;
    private bool _disposed;
    internal Action<string> BrowserLauncher { get; init; } = url =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public StdioSignInResult? PendingSignIn =>
        sessionManager.HasSession() || _signInTask.IsCompleted ? null : _pendingSignIn;

    public async Task<StdioSignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        await _signInLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (sessionManager.HasSession())
                return new StdioSignInResult("Ok", Message: "An active Security Center session is already available.");

            if (!_signInTask.IsCompleted)
                return _pendingSignIn!;

            await _signInTask;
            _timeout?.Dispose();
            return await SignInCoreAsync(cancellationToken);
        }
        finally
        {
            _signInLock.Release();
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        await _signInLock.WaitAsync(cancellationToken);
        try
        {
            if (!_signInTask.IsCompleted)
                _timeout?.Cancel();
            await _signInTask;
            _timeout?.Dispose();
            _timeout = null;
            _pendingSignIn = null;
        }
        finally
        {
            _signInLock.Release();
        }
    }

    private async Task<StdioSignInResult> SignInCoreAsync(CancellationToken cancellationToken)
    {
        // The listener must outlive the connect request so the returned URL remains usable.
        var expiresAt = DateTimeOffset.UtcNow.Add(SignInLifetime);
        var timeout = new CancellationTokenSource(SignInLifetime);
        var csrfToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        const string route = "/authorize";
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateSlimBuilder();
        // Ignore the application's HTTP endpoints and logging configuration for this local listener.
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, 0);
        });
        var rateLimitingEnabled = configuration.GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>()?.Enabled ?? true;
        builder.Services.AddRateLimitingOptions(configuration);
        if (rateLimitingEnabled)
            builder.Services.AddAuthorizationThrottling();
        var app = builder.Build();
        app.UseRouting();
        if (rateLimitingEnabled)
            app.UseRateLimiter();

        string Page(string scope, string? error = null, string host = "") =>
            AuthorizeEndpoint.BuildLoginPage("", "", "", "", "", scope,
                host: AuthorizeEndpoint.GetSingleAllowedHostForDisplay(options.Value) ?? host,
                hostReadOnly: AuthorizeEndpoint.GetSingleAllowedHostForDisplay(options.Value) is not null,
                errorMessage: error, connectionOptions: options.Value.WebSdkSettings,
                showBrowserHttpWarning: false, selectAllWhenEmpty: error is null, csrfToken: csrfToken);

        app.MapGet(route, (HttpContext context) =>
        {
            AuthorizeEndpoint.SetFrameProtection(context.Response);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Content(Page(string.Join(' ',
            sessionManager.StdioScopes.Select(scope =>
                System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(scope.ToString())))), "text/html");
        });
        app.MapPost(route, async (HttpContext context) =>
        {
            AuthorizeEndpoint.SetFrameProtection(context.Response);
            context.Response.Headers.CacheControl = "no-store";
            // Match the browser-facing loopback origin, including forwarded local ports.
            var signInOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            if (!Uri.TryCreate(signInOrigin, UriKind.Absolute, out var requestUri) || !requestUri.IsLoopback)
                return Results.BadRequest("Invalid sign-in request. Open the sign-in link and try again.");
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, signInOrigin, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Invalid sign-in request. Open the sign-in link and try again.");
            var referer = context.Request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(origin) && !string.IsNullOrEmpty(referer) &&
                (!Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) ||
                 !string.Equals(refererUri.GetLeftPart(UriPartial.Authority), signInOrigin, StringComparison.OrdinalIgnoreCase)))
                return Results.BadRequest("Invalid sign-in request. Open the sign-in link and try again.");
            var form = await context.Request.ReadFormAsync(timeout.Token);
            var submittedToken = form["csrf_token"].ToString();
            if (submittedToken.Length != csrfToken.Length || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(submittedToken), Encoding.UTF8.GetBytes(csrfToken)))
                return Results.BadRequest("Invalid sign-in request. Open the sign-in link and try again.");
            var scope = AuthorizeEndpoint.NormalizeScope(form["scope"].ToString());
            var (connection, error) = await AuthorizeEndpoint.ValidateLoginForm(form, options.Value, sessionManager);
            timeout.Token.ThrowIfCancellationRequested();
            if (connection is null)
                return Results.Content(Page(scope, error, form["host"].ToString().Trim()), "text/html");

            sessionManager.Logout();
            sessionManager.SetStdioClientId(connection.User, $"{connection.Host}:{connection.Port}");
            sessionManager.Connect("default", connection);
            sessionManager.StdioScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(ScopedToolFilter.ParseScope).OfType<ToolScope>().ToHashSet();
            // Kestrel's graceful shutdown lets this response finish before closing.
            completion.TrySetResult();
            return Results.Text("Sign-in complete. Return to your MCP client and close this tab.");
        }).RequireRateLimiting(AuthorizeEndpoint.RateLimitPolicy);
        app.MapLoginImages();

        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await app.StartAsync(startup.Token);
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
            var url = address + route;
            var browserOpened = true;
            try
            {
                BrowserLauncher(url);
            }
            catch
            {
                browserOpened = false;
            }
            _pendingSignIn = new StdioSignInResult(
                "Pending", url, expiresAt, browserOpened,
                "Open sign_in_url in your browser and complete sign-in. " +
                "Then call session_management with action 'list' and confirm an active session before continuing. " +
                "The link expires in five minutes and must be opened on the machine hosting the MCP server, " +
                "or through a forwarded port when using a remote host.");
            _timeout = timeout;
            _signInTask = CompleteSignInAsync(app, completion.Task, timeout);
            return _pendingSignIn;
        }
        catch
        {
            timeout.Dispose();
            await app.DisposeAsync();
            throw;
        }
    }

    private static async Task CompleteSignInAsync(WebApplication app, Task completion, CancellationTokenSource timeout)
    {
        try
        {
            await completion.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // Timeout, logout and host shutdown all close the temporary listener.
        }
        finally
        {
            try
            {
                await app.StopAsync(CancellationToken.None);
            }
            finally
            {
                await app.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _signInLock.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!_signInTask.IsCompleted)
                _timeout?.Cancel();
            await _signInTask;
            _timeout?.Dispose();
            _timeout = null;
        }
        finally
        {
            _signInLock.Release();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}

public sealed record StdioSignInResult(
    string Status,
    string? SignInUrl = null,
    DateTimeOffset? ExpiresAt = null,
    bool? BrowserOpened = null,
    string? Message = null);
