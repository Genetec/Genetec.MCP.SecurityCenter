using Genetec.Mcp.SecurityCenter.Configuration;

namespace Genetec.Mcp.SecurityCenter.Authentication;

/// <summary>Persists the signing secret in appsettings.json using a dedicated Data Protection key ring.</summary>
internal static class ProtectedSigningKeyStore
{
    public static async Task<string> LoadOrCreateAsync(SigningKeyStoreOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConfigPath);
        var configPath = Path.GetFullPath(options.ConfigPath);
        var keyRingPath = options.KeyRingPath ?? GetDefaultKeyRingPath(configPath);
        if (!Path.IsPathFullyQualified(keyRingPath))
            throw new ArgumentException("The Data Protection key-ring path must be absolute.", nameof(options));
        keyRingPath = Path.GetFullPath(keyRingPath);
        var root = await SigningKeyConfigurationFile.ReadAsync(configPath, cancellationToken).ConfigureAwait(false);
        var authentication = root.Authentication;
        var protectedKey = authentication.ProtectedSigningKey;
        if (protectedKey is not null)
            return SigningKeyDataProtection.Unprotect(protectedKey, keyRingPath, options.Provider);

        cancellationToken.ThrowIfCancellationRequested();
        var created = SigningKeyDataProtection.CreateKey(keyRingPath, options.Provider);
        authentication.ProtectedSigningKey = created.ProtectedSigningKey;
        await SigningKeyConfigurationFile.WriteAsync(configPath, root, cancellationToken).ConfigureAwait(false);
        return created.SigningKey;
    }

    internal static string GetDefaultKeyRingPath(string configPath)
    {
        if (!OperatingSystem.IsLinux())
            return Path.Combine(Path.GetDirectoryName(configPath)!, "keys");
        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            return "/app/config/keys";

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Path.IsPathFullyQualified(userProfile))
            throw new InvalidOperationException("The service account home directory is unavailable. Configure Authentication:SigningKeyProtection:KeyRingPath.");
        return Path.Combine(userProfile, ".config", "genetec", "mcp", "keys");
    }
}

internal sealed record SigningKeyStoreOptions
{
    public required string ConfigPath { get; init; }
    public SigningKeyProtectionProvider Provider { get; init; } = SigningKeyProtectionProvider.Auto;
    public string? KeyRingPath { get; init; }
}
