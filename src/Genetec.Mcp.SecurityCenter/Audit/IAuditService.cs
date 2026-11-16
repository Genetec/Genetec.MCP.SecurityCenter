using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.Audit;

public interface IAuditService
{
    void LogCall(string clientId, string toolName, IDictionary<string, JsonElement>? arguments, string outcome, long durationMs, string? errorMessage = null);
    void LogException(string clientId, string toolName, IDictionary<string, JsonElement>? arguments, string exceptionMessage, long durationMs);
    void LogResourceRead(string clientId, string resourceUri, string outcome, long durationMs);
    void LogResourceReadException(string clientId, string resourceUri, string exceptionMessage, long durationMs);
}
