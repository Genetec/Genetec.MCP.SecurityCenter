using System.Security.Cryptography;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Genetec.Mcp.SecurityCenter.Authentication;

internal static class SigningKeyDataProtection
{
    internal const string ApplicationName = "Genetec.Mcp.SecurityCenter";
    internal const string Purpose = "SecurityCenter.SigningKey.v1";
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    internal static (string SigningKey, string ProtectedSigningKey) CreateKey(
        string keyRingPath, SigningKeyProtectionProvider provider)
    {
        using var services = CreateProvider(keyRingPath, provider, initialize: true);
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
        var random = RandomNumberGenerator.GetBytes(32);
        try
        {
            var signingKey = Convert.ToBase64String(random);
            try { return (signingKey, protector.Protect(signingKey)); }
            finally { RestrictNewKeyFiles(keyRingPath); }
        }
        finally { CryptographicOperations.ZeroMemory(random); }
    }

    internal static string Unprotect(string protectedSigningKey, string keyRingPath, SigningKeyProtectionProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSigningKey);
        using var services = CreateProvider(keyRingPath, provider, initialize: false);
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
        return ValidateCanonicalText(protector.Unprotect(protectedSigningKey));
    }

    internal static SigningKeyProtectionProvider ResolveProvider(SigningKeyProtectionProvider provider)
    {
        if (provider == SigningKeyProtectionProvider.Auto)
            provider = OperatingSystem.IsWindows() ? SigningKeyProtectionProvider.WindowsDpapi
                : OperatingSystem.IsLinux() ? SigningKeyProtectionProvider.FileSystem
                : throw new PlatformNotSupportedException("Automatic key-ring protection requires Windows or Linux.");

        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        if (provider == SigningKeyProtectionProvider.WindowsDpapi && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The key-ring protection provider does not match this operating system.");
        return provider;
    }

    internal static string[] PrepareDirectory(string path, bool initialize)
    {
        SigningKeyConfigurationFile.EnsureNoSymbolicLinks(path);
        if (!Directory.Exists(path))
        {
            if (!initialize) throw new DirectoryNotFoundException("The signing-key Data Protection key ring is missing.");
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
            else Directory.CreateDirectory(path, DirectoryMode);
        }
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != DirectoryMode)
            throw new UnauthorizedAccessException("The Data Protection key-ring directory must have owner-only permissions (0700).");

        var files = Directory.GetFiles(path);
        foreach (var file in files)
        {
            SigningKeyConfigurationFile.EnsureSafeFile(file);
            var name = Path.GetFileName(file);
            if (!(name.StartsWith("key-", StringComparison.Ordinal) || name.StartsWith("revocation-", StringComparison.Ordinal))
                || !name.EndsWith(".xml", StringComparison.Ordinal))
                throw new InvalidDataException("The signing-key directory must contain only Data Protection key files.");
            if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(file) != FileMode)
                throw new UnauthorizedAccessException("Data Protection key files must have owner-only permissions (0600).");
        }
        return files.Where(file => Path.GetFileName(file).StartsWith("key-", StringComparison.Ordinal)).ToArray();
    }

    internal static ServiceProvider CreateProvider(string path, SigningKeyProtectionProvider protection, bool initialize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("The Data Protection key-ring path must be absolute.", nameof(path));
        protection = ResolveProvider(protection);
        var keyFiles = PrepareDirectory(path, initialize);
        if (!initialize && keyFiles.Length == 0)
            throw new CryptographicException("The signing-key Data Protection key ring is empty.");

        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(path));
        if (!initialize) builder.DisableAutomaticKeyGeneration();
        if (protection == SigningKeyProtectionProvider.WindowsDpapi)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            builder.ProtectKeysWithDpapi();
        }
        return services.BuildServiceProvider();
    }

    internal static void RestrictNewKeyFiles(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        // The 0700 directory protects files during the framework's atomic write.
        foreach (var file in Directory.GetFiles(path, "key-*.xml"))
        {
            SigningKeyConfigurationFile.EnsureSafeFile(file);
            File.SetUnixFileMode(file, FileMode);
        }
    }

    private static string ValidateCanonicalText(string value)
    {
        byte[] raw;
        try { raw = Convert.FromBase64String(value); }
        catch (FormatException exception) { throw new CryptographicException("The protected signing key is invalid.", exception); }
        try
        {
            if (raw.Length != 32 || Convert.ToBase64String(raw) != value)
                throw new CryptographicException("The protected signing key is invalid.");
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
}
