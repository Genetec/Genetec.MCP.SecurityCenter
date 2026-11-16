using Serilog.Context;

namespace Genetec.Mcp.SecurityCenter.Audit;

public class CorrelationIdMiddleware
{
    private const string Header = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[Header].FirstOrDefault()
            ?? context.TraceIdentifier;

        if (correlationId.Length == 0 || correlationId.Length > 128 || correlationId.Any(char.IsControl))
            correlationId = context.TraceIdentifier;

        context.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
