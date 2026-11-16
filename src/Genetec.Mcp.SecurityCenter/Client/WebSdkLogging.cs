namespace Genetec.Mcp.SecurityCenter.Client;

internal static class WebSdkLogging
{
    internal const string CategoryName = "WebSdk";

    internal static void LogResponse(
        ILogger logger, HttpRequestMessage request, HttpResponseMessage? response,
        string? body, long durationMs, Exception? exception = null)
    {
        if (response is null)
        {
            logger.LogWarning(exception,
                "Web SDK {Method} {Endpoint} failed after {DurationMs}ms",
                request.Method.Method, GetEndpoint(request), durationMs);
            return;
        }

        if (!response.IsSuccessStatusCode || exception is not null)
        {
            logger.LogWarning(exception,
                "Web SDK {Method} {Endpoint} failed (HTTP {StatusCode}) in {DurationMs}ms | ResponseBody={ResponseBody}",
                request.Method.Method, GetEndpoint(request), (int)response.StatusCode, durationMs, body);
            return;
        }

        logger.LogInformation(
            "Web SDK {Method} {Endpoint} returned HTTP {StatusCode} in {DurationMs}ms",
            request.Method.Method, GetEndpoint(request), (int)response.StatusCode, durationMs);
        if (body is not null && logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Web SDK response body: {ResponseBody}", body);
    }

    internal static void LogEvent(ILogger logger, string body)
    {
        if (logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Web SDK event body: {ResponseBody}", body);
    }

    // SchemeAndServer excludes user information; Path excludes query values and fragments.
    private static string? GetEndpoint(HttpRequestMessage request) =>
        request.RequestUri?.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
}
