using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Client;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools;

[McpServerToolType]
public class SessionManagementTool
{
    [McpServerTool(
        Name = "session_management",
        Title = "Session Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Manage the Security Center session. " +
        "Use connect to start browser sign-in (stdio only); an existing session is reused. " +
        "If pending, show sign_in_url to the user, wait for sign-in, then call list to confirm an active session. " +
        "logout clears the current session and cancels pending sign-in; list inspects current state.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        IServiceProvider services,
        [Description("Session management request")] SessionManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var signInService = services.GetService<StdioSignInService>();
        switch (request.Action)
        {
            case SessionAction.List:
                return new
                {
                    Sessions = sessionManager.ListSessions(),
                    Active = sessionManager.ActiveAlias,
                    SignIn = signInService?.PendingSignIn
                };

            case SessionAction.Connect:
                if (services.GetService<IHttpContextAccessor>() is not null)
                    throw new ArgumentException("connect is not supported in HTTP mode — use the OAuth sign-in flow.");
                if (signInService is null)
                    throw new InvalidOperationException("Browser sign-in is unavailable in this host.");
                return await signInService.SignInAsync(cancellationToken);

            case SessionAction.Logout:
                if (signInService is not null)
                    await signInService.CancelAsync(cancellationToken);
                sessionManager.Logout();
                return new StatusResult("Ok");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}", "action");
        }
    }

}

public enum SessionAction { List, Connect, Logout }

public sealed record SessionManagementRequest(
    [property: Description("Action to perform"), Required] SessionAction Action);
