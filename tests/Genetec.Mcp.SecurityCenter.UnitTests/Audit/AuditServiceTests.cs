using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Audit;
using Microsoft.Extensions.Logging;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Audit;

public class AuditServiceTests
{
    private readonly CapturingLogger _log = new();
    private readonly AuditService _svc;

    public AuditServiceTests() =>
        _svc = new AuditService(new CapturingLoggerFactory(_log));

    private static IDictionary<string, JsonElement> Args(params (string Key, string Value)[] pairs)
    {
        var doc = JsonDocument.Parse(
            "{" + string.Join(",", pairs.Select(p => $"\"{p.Key}\":\"{p.Value}\"")) + "}");
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    // ── Redaction ────────────────────────────────────────────────────

    [Fact]
    public void LogCall_NullArguments_LogsEmptyObject()
    {
        _svc.LogCall("c", "t", null, "Success", 1);
        Assert.Contains("{}", _log.Last.Message);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("pin")]
    [InlineData("client_secret")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("raw_data")]
    [InlineData("signing_key")]
    [InlineData("csn")]
    [InlineData("custom_values")]
    [InlineData("access_token")]
    [InlineData("bearer_token")]
    [InlineData("code_verifier")]
    public void LogCall_SensitiveParam_Redacted(string paramName)
    {
        _svc.LogCall("c", "t", Args((paramName, "super-secret")), "Success", 1);
        Assert.Contains("***REDACTED***", _log.Last.Message);
        Assert.DoesNotContain("super-secret", _log.Last.Message);
    }

    [Fact]
    public void LogCall_NonSensitiveParam_PassedThrough()
    {
        _svc.LogCall("c", "t", Args(("id", "abc-123")), "Success", 1);
        Assert.Contains("abc-123", _log.Last.Message);
    }

    [Fact]
    public void LogCall_MixedParams_OnlySensitiveRedacted()
    {
        _svc.LogCall("c", "t", Args(("id", "abc-123"), ("password", "s3cr3t")), "Success", 1);
        Assert.Contains("abc-123", _log.Last.Message);
        Assert.Contains("***REDACTED***", _log.Last.Message);
        Assert.DoesNotContain("s3cr3t", _log.Last.Message);
    }

    [Theory]
    [InlineData("session_management", false)]
    [InlineData("session_management", true)]
    [InlineData("user_security", false)]
    [InlineData("user_security", true)]
    public void NestedRequest_PasswordRedacted_InSuccessAndExceptionAudits(string toolName, bool exception)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"request":{"Action":"Connect","Password":"nested-secret","Id":"abc-123"}}""")!;

        if (exception)
            _svc.LogException("c", toolName, arguments, "boom", 1);
        else
            _svc.LogCall("c", toolName, arguments, "Success", 1);

        Assert.DoesNotContain("nested-secret", _log.Last.Message);
        using var logged = LoggedArguments();
        var request = logged.RootElement.GetProperty("request");
        Assert.Equal("***REDACTED***", request.GetProperty("Password").GetString());
        Assert.Equal("abc-123", request.GetProperty("Id").GetString());
        Assert.Equal(JsonValueKind.Object, request.ValueKind);
        Assert.Equal("nested-secret", arguments["request"].GetProperty("Password").GetString());
    }

    [Theory]
    [InlineData("Pin")]
    [InlineData("CustomValues")]
    [InlineData("custom_values")]
    [InlineData("ClientSecret")]
    [InlineData("client_secret")]
    [InlineData("RAW_DATA")]
    [InlineData("RawData")]
    [InlineData("SigningKey")]
    [InlineData("AccessToken")]
    [InlineData("BearerToken")]
    [InlineData("CodeVerifier")]
    public void NestedArrays_RedactSensitiveFieldsAndPreserveJsonTypes(string field)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            $$$"""{"request":{"items":[[{"{{{field}}}":{"value":"batch-secret"},"count":2,"enabled":true,"optional":null}],"public",3,false]}}""")!;

        _svc.LogCall("c", "credential_create", arguments, "Success", 1);

        Assert.DoesNotContain("batch-secret", _log.Last.Message);
        using var logged = LoggedArguments();
        var items = logged.RootElement.GetProperty("request").GetProperty("items");
        var item = items[0][0];
        Assert.Equal("***REDACTED***", item.GetProperty(field).GetString());
        Assert.Equal(2, item.GetProperty("count").GetInt32());
        Assert.True(item.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("optional").ValueKind);
        Assert.Equal("public", items[1].GetString());
        Assert.Equal(3, items[2].GetInt32());
        Assert.False(items[3].GetBoolean());
    }

    [Fact]
    public void DeeplyNestedObjects_RedactMixedCaseSecrets()
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"request":{"configuration":{"connection":{"credentials":{"pAsSwOrD":"deep-secret","Username":"operator"}}}}} """)!;

        _svc.LogCall("c", "session_management", arguments, "Success", 1);

        Assert.DoesNotContain("deep-secret", _log.Last.Message);
        using var logged = LoggedArguments();
        var credentials = logged.RootElement.GetProperty("request").GetProperty("configuration")
            .GetProperty("connection").GetProperty("credentials");
        Assert.Equal("***REDACTED***", credentials.GetProperty("pAsSwOrD").GetString());
        Assert.Equal("operator", credentials.GetProperty("Username").GetString());
    }

    [Fact]
    public void SensitiveFields_RedactedRegardlessOfValueType()
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"request":{"Password":null,"Pin":1234,"Token":true,"Secret":"","CustomValues":["hidden-value"]}}""")!;

        _svc.LogCall("c", "credential_create", arguments, "Success", 1);

        using var logged = LoggedArguments();
        foreach (var property in logged.RootElement.GetProperty("request").EnumerateObject())
            Assert.Equal("***REDACTED***", property.Value.GetString());
        Assert.DoesNotContain("1234", _log.Last.Message);
        Assert.DoesNotContain("hidden-value", _log.Last.Message);
    }

    [Fact]
    public void EscapedPropertyNames_RedactedAndOrdinaryStringsRoundTrip()
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"request":{"\u0050assword":"escaped-secret","description":"Door \"A\"\nC:\\devices\\入口"}}""")!;

        _svc.LogCall("c", "user_security", arguments, "Success", 1);

        Assert.DoesNotContain("escaped-secret", _log.Last.Message);
        using var logged = LoggedArguments();
        var request = logged.RootElement.GetProperty("request");
        Assert.Equal("***REDACTED***", request.GetProperty("Password").GetString());
        Assert.Equal(arguments["request"].GetProperty("description").GetString(),
            request.GetProperty("description").GetString());
    }

    [Fact]
    public void ExceptionAudit_TopLevelBatch_RedactsEveryItem()
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"items":[{"Id":"first","Password":"first-secret"},{"Id":"second","Pin":"second-secret"}],"emptyArray":[],"emptyObject":{}}""")!;

        _svc.LogException("c", "credential_create", arguments, "boom", 1);

        Assert.DoesNotContain("first-secret", _log.Last.Message);
        Assert.DoesNotContain("second-secret", _log.Last.Message);
        using var logged = LoggedArguments();
        var items = logged.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("first", items[0].GetProperty("Id").GetString());
        Assert.Equal("second", items[1].GetProperty("Id").GetString());
        Assert.Equal("***REDACTED***", items[0].GetProperty("Password").GetString());
        Assert.Equal("***REDACTED***", items[1].GetProperty("Pin").GetString());
        Assert.Equal(0, logged.RootElement.GetProperty("emptyArray").GetArrayLength());
        Assert.Empty(logged.RootElement.GetProperty("emptyObject").EnumerateObject());
        Assert.Equal(LogLevel.Error, _log.Last.Level);
    }

    [Fact]
    public void SimilarNonSensitiveNames_ArePreserved()
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"request":{"PasswordPolicy":"strong","TokenCount":2,"PinRequired":true,"SecretDescription":"metadata"}}""")!;

        _svc.LogCall("c", "t", arguments, "Success", 1);

        using var logged = LoggedArguments();
        Assert.True(JsonElement.DeepEquals(arguments["request"], logged.RootElement.GetProperty("request")));
        Assert.DoesNotContain("***REDACTED***", _log.Last.Message);
    }

    private JsonDocument LoggedArguments()
    {
        var message = _log.Last.Message;
        var start = message.IndexOf("Args=", StringComparison.Ordinal) + "Args=".Length;
        var end = message.IndexOf(" | Outcome=", start, StringComparison.Ordinal);
        return JsonDocument.Parse(message[start..end]);
    }

    // ── Log content ──────────────────────────────────────────────────

    [Fact]
    public void LogCall_WritesExpectedFields()
    {
        _svc.LogCall("client-1", "camera_get", null, "Success", 42);
        var msg = _log.Last.Message;
        Assert.Contains("AUDIT", msg);
        Assert.Contains("client-1", msg);
        Assert.Contains("camera_get", msg);
        Assert.Contains("Success", msg);
        Assert.Equal(LogLevel.Information, _log.Last.Level);
    }

    [Fact]
    public void LogException_WritesAtErrorWithExceptionMessage()
    {
        _svc.LogException("client-1", "camera_get", null, "boom", 5);
        var msg = _log.Last.Message;
        Assert.Contains("AUDIT", msg);
        Assert.Contains("boom", msg);
        Assert.Equal(LogLevel.Error, _log.Last.Level);
    }
}

// ── Test infrastructure ──────────────────────────────────────────────────────

internal sealed class CapturingLoggerFactory(CapturingLogger logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => logger;
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
}

internal sealed class CapturingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public (LogLevel Level, string Message) Last => _entries[^1];
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add((logLevel, formatter(state, exception)));

    public bool IsEnabled(LogLevel logLevel) => true;
    IDisposable? ILogger.BeginScope<TState>(TState state) => null;
}
