using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Configuration;

namespace Genetec.Mcp.SecurityCenter.Authentication;

/// <summary>Loads the signing secret before the HTTP server starts listening.</summary>
internal static class HttpSigningKeyStartup
{
    public static void RejectPlaintextSigningKey(IConfiguration configuration)
    {
        const string path = $"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.SigningKey)}";
        var configured = configuration is IConfigurationRoot root
            ? root.Providers.Any(provider => provider.TryGet(path, out _))
            : configuration[path] is not null;
        if (configured)
            throw new InvalidOperationException($"{path} cannot be supplied through configuration. Use Data Protection.");
    }

    public static Task<string> LoadAsync(IConfiguration configuration, string configPath, CancellationToken cancellationToken = default)
    {
        RejectPlaintextSigningKey(configuration);
        var settings = configuration.GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();
        var protection = settings.SigningKeyProtection
            ?? throw new InvalidOperationException($"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.SigningKeyProtection)} settings are invalid.");
        return ProtectedSigningKeyStore.LoadOrCreateAsync(new SigningKeyStoreOptions
        {
            ConfigPath = configPath,
            Provider = protection.Provider,
            KeyRingPath = protection.KeyRingPath
        }, cancellationToken);
    }
}
