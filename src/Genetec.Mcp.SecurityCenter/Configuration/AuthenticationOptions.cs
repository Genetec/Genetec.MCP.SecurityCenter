using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Configuration;

public class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public const int DefaultTokenLifetimeMinutes = 60;
    public int TokenLifetimeMinutes { get; set; } = DefaultTokenLifetimeMinutes;
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    // The decrypted key is supplied at runtime and must never be serialized.
    [JsonIgnore]
    public virtual string SigningKey { get; set; } = "";

    public string Issuer { get; set; } = "GenetecSecurityCenter";
    public string Audience { get; set; } = "GenetecSecurityCenter";
    public SigningKeyProtectionSettings SigningKeyProtection { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SigningKeyProtectionSettings
{
    public SigningKeyProtectionProvider Provider { get; set; } = SigningKeyProtectionProvider.Auto;
    public string? KeyRingPath { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
