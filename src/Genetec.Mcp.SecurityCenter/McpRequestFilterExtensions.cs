using System.Diagnostics;
using Genetec.Mcp.SecurityCenter.Audit;
using Genetec.Mcp.SecurityCenter.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter;

public static class McpRequestFilterExtensions
{
    public static IMcpRequestFilterBuilder AddScopedToolFilter(this IMcpRequestFilterBuilder filters)
    {
        var map = ScopedToolFilter.BuildToolScopeMap(typeof(ScopedToolFilter).Assembly);
        filters.AddListToolsFilter(ScopedToolFilter.CreateFilter(map));
        filters.AddCallToolFilter(next => async (context, ct) =>
        {
            var scopeClaim = context.Server.Services
                ?.GetService<IHttpContextAccessor>()
                ?.HttpContext?.User.FindFirst("scope")?.Value ?? "";

            var grantedScopes = scopeClaim
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(ScopedToolFilter.ParseScope)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToHashSet();

            var toolName = context.Params?.Name ?? "";
            if (map.TryGetValue(toolName, out var toolScope))
            {
                if (grantedScopes.Count == 0)
                    return Error("Access denied: token contains no recognized tool scopes.");
                if (!ScopedToolFilter.IsScopeAllowed(grantedScopes, toolScope))
                    return Error($"Access denied: token scope does not permit calling '{toolName}'.");
            }

            return await next(context, ct);
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddScopedToolFilter(
        this IMcpRequestFilterBuilder filters,
        HashSet<ToolScope> allowedScopes)
        => filters.AddScopedToolFilter(() => allowedScopes);

    public static IMcpRequestFilterBuilder AddScopedToolFilter(
        this IMcpRequestFilterBuilder filters,
        Func<HashSet<ToolScope>> getAllowedScopes,
        Func<bool>? hasSession = null)
    {
        var map = ScopedToolFilter.BuildToolScopeMap(typeof(ScopedToolFilter).Assembly);
        // Stdio clients may cache discovery once; enforce selected scopes when tools are called.
        filters.AddCallToolFilter(next => async (context, ct) =>
        {
            var toolName = context.Params?.Name ?? "";
            if (hasSession?.Invoke() == false && toolName != "session_management" && toolName != "search_tools")
                return Error("No active Security Center session. Call session_management with action 'connect' to sign in.");

            var allowedScopes = getAllowedScopes();
            if (allowedScopes.Count > 0 && map.TryGetValue(toolName, out var toolScope) && !ScopedToolFilter.IsScopeAllowed(allowedScopes, toolScope))
                return Error($"Access denied: tool '{toolName}' is not available in this session.");

            return await next(context, ct);
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddSchemaStripFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddListToolsFilter(ScopedToolFilter.CreateSchemaStripFilter());
        return filters;
    }

    public static IMcpRequestFilterBuilder AddThrottleCallFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddCallToolFilter(next => async (context, ct) =>
        {
            var throttle = context.Server.Services?.GetService<ThrottleService>();
            if (throttle is null)
                return await next(context, ct);

            var userId = context.Server.Services
                ?.GetService<IHttpContextAccessor>()
                ?.HttpContext?.User.FindFirst("sub")?.Value ?? "__anonymous__";

            using var acquisition = await throttle.TryAcquireAsync(userId, ct);
            if (!acquisition.IsAcquired)
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = acquisition.RejectionMessage! }]
                };

            return await next(context, ct);
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddThrottleResourceFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddReadResourceFilter(next => async (context, ct) =>
        {
            var throttle = context.Server.Services?.GetService<ThrottleService>();
            if (throttle is null)
                return await next(context, ct);

            var userId = context.Server.Services
                ?.GetService<IHttpContextAccessor>()
                ?.HttpContext?.User.FindFirst("sub")?.Value ?? "__anonymous__";

            using var acquisition = await throttle.TryAcquireAsync(userId, ct);
            if (!acquisition.IsAcquired)
                throw new ModelContextProtocol.McpProtocolException(acquisition.RejectionMessage!);

            return await next(context, ct);
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddErrorHandlerCallFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddCallToolFilter(next => async (context, ct) =>
        {
            try
            {
                return await next(context, ct);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Error($"Invalid parameter: {ex.Message}");
            }
            catch (System.ComponentModel.DataAnnotations.ValidationException ex)
            {
                return Error($"Validation failed: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return Error(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Error($"Security Center request failed: {ex.Message}");
            }
            catch (IOException ex)
            {
                return Error(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Error(ex.Message);
            }
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddAuditCallFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddCallToolFilter(next => async (context, ct) =>
        {
            var toolName = context.Params?.Name ?? "unknown";
            var auditService = context.Server.Services?.GetService<IAuditService>();
            var clientId = context.Server.Services
                ?.GetService<SecurityCenterSessionManager>()
                ?.CurrentClientId ?? "__anonymous__";
            var arguments = context.Params?.Arguments;

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await next(context, ct);
                sw.Stop();
                var isError = result.IsError == true;
                var errorMessage = isError
                    ? string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text))
                    : null;
                auditService?.LogCall(clientId, toolName, arguments, isError ? "ToolError" : "Success", sw.ElapsedMilliseconds, errorMessage);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                auditService?.LogException(clientId, toolName, arguments, ex.Message, sw.ElapsedMilliseconds);
                throw;
            }
        });
        return filters;
    }

    public static IMcpRequestFilterBuilder AddAuditResourceFilter(this IMcpRequestFilterBuilder filters)
    {
        filters.AddReadResourceFilter(next => async (context, ct) =>
        {
            var resourceUri = context.Params?.Uri ?? "unknown";
            var auditService = context.Server.Services?.GetService<IAuditService>();
            var clientId = context.Server.Services
                ?.GetService<SecurityCenterSessionManager>()
                ?.CurrentClientId ?? "__anonymous__";

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await next(context, ct);
                sw.Stop();
                auditService?.LogResourceRead(clientId, resourceUri, "Success", sw.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                auditService?.LogResourceReadException(clientId, resourceUri, ex.Message, sw.ElapsedMilliseconds);
                throw;
            }
        });
        return filters;
    }

    private static CallToolResult Error(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
}
