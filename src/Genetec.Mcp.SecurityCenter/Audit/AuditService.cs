using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.Audit;

public sealed class AuditService : IAuditService
{
    private readonly ILogger _logger;

    private static readonly HashSet<string> SensitiveParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pin", "client_secret", "secret", "token",
        "raw_data", "signing_key", "csn", "custom_values",
        "access_token", "bearer_token", "code_verifier",
        "clientSecret", "rawData", "signingKey", "customValues",
        "accessToken", "bearerToken", "codeVerifier"
    };

    public AuditService(ILoggerFactory loggerFactory) =>
        _logger = loggerFactory.CreateLogger("Audit");

    public void LogCall(string clientId, string toolName, IDictionary<string, JsonElement>? arguments, string outcome, long durationMs, string? errorMessage = null)
    {
        if (outcome == "ToolError")
        {
            _logger.LogError(
                "AUDIT | {Timestamp:o} | Client={ClientId} | Tool={ToolName} | Args={Arguments} | Outcome={Outcome} | Error={ErrorMessage} | Duration={DurationMs}ms",
                DateTimeOffset.UtcNow, clientId, toolName, Redact(arguments), outcome, errorMessage, durationMs);
            return;
        }

        _logger.LogInformation(
            "AUDIT | {Timestamp:o} | Client={ClientId} | Tool={ToolName} | Args={Arguments} | Outcome={Outcome} | Duration={DurationMs}ms",
            DateTimeOffset.UtcNow, clientId, toolName, Redact(arguments), outcome, durationMs);
    }

    public void LogException(string clientId, string toolName, IDictionary<string, JsonElement>? arguments, string exceptionMessage, long durationMs) =>
        _logger.LogError(
            "AUDIT | {Timestamp:o} | Client={ClientId} | Tool={ToolName} | Args={Arguments} | Outcome=Error | Exception={Exception} | Duration={DurationMs}ms",
            DateTimeOffset.UtcNow, clientId, toolName, Redact(arguments), exceptionMessage, durationMs);

    public void LogResourceRead(string clientId, string resourceUri, string outcome, long durationMs) =>
        _logger.LogInformation(
            "AUDIT | {Timestamp:o} | Client={ClientId} | Resource={ResourceUri} | Outcome={Outcome} | Duration={DurationMs}ms",
            DateTimeOffset.UtcNow, clientId, resourceUri, outcome, durationMs);

    public void LogResourceReadException(string clientId, string resourceUri, string exceptionMessage, long durationMs) =>
        _logger.LogError(
            "AUDIT | {Timestamp:o} | Client={ClientId} | Resource={ResourceUri} | Outcome=Error | Exception={Exception} | Duration={DurationMs}ms",
            DateTimeOffset.UtcNow, clientId, resourceUri, exceptionMessage, durationMs);

    private static string Redact(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0) return "{}";
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in arguments)
                WriteRedactedProperty(writer, key, value);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteRedactedProperty(Utf8JsonWriter writer, string name, JsonElement value)
    {
        writer.WritePropertyName(name);
        if (SensitiveParams.Contains(name))
            writer.WriteStringValue("***REDACTED***");
        else
            WriteRedactedValue(writer, value);
    }

    private static void WriteRedactedValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                    WriteRedactedProperty(writer, property.Name, property.Value);
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteRedactedValue(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
