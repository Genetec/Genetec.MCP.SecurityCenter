using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.WebUtilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class ProtectedSigningKeyStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mcp-data-protection-tests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(directory, "config", "appsettings.json");
    private string KeyRingPath => Path.Combine(directory, "keys");
    private SigningKeyStoreOptions Options => new()
    {
        ConfigPath = ConfigPath,
        KeyRingPath = KeyRingPath,
        Provider = SigningKeyProtectionProvider.FileSystem
    };

    private async Task InitializeConfigurationAsync(string json = "{\"SecurityCenter\":{\"TestValue\":\"preserved\"},\"Authentication\":{\"Issuer\":\"test-issuer\"}}")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    [Fact]
    public async Task FirstStartAndRecreationReusePersistedSecretAcrossApplicationPaths()
    {
        await InitializeConfigurationAsync();
        var first = await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        var written = await File.ReadAllTextAsync(ConfigPath);
        var root = JsonNode.Parse(written)!;
        Assert.Equal(32, Convert.FromBase64String(first).Length);
        Assert.DoesNotContain(first, written);
        Assert.Equal("preserved", root["SecurityCenter"]!["TestValue"]!.GetValue<string>());
        Assert.Equal("test-issuer", root["Authentication"]!["Issuer"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(root["Authentication"]!["ProtectedSigningKey"]!.GetValue<string>()));
        var keys = Directory.GetFiles(KeyRingPath);
        Assert.Single(keys);
        Assert.EndsWith(".xml", keys[0]);
        var keyContent = await File.ReadAllTextAsync(keys[0]);
        Assert.Equal(first, await ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Equal(written, await File.ReadAllTextAsync(ConfigPath));
        Assert.Equal(keyContent, await File.ReadAllTextAsync(keys[0]));

        var otherPath = Path.Combine(directory, "recreated", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath)!);
        File.Copy(ConfigPath, otherPath);
        Assert.Equal(first, await ProtectedSigningKeyStore.LoadOrCreateAsync(Options with { ConfigPath = otherPath }));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(keys[0]));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(KeyRingPath));
        }
    }

    [Theory]
    [InlineData("{\"Authentication\":{\"SigningKey\":\"known-key\"}}")]
    [InlineData("{\"authentication\":{\"signingkey\":null}}")]
    [InlineData("{\"Authentication\":{},\"authentication\":{}}")]
    [InlineData("{\"Authentication\":{},\"Authentication\":{}}")]
    [InlineData("{\"Authentication\":null}")]
    [InlineData("{\"Authentication\":{\"Issuer\":\"first\",\"issuer\":\"second\"}}")]
    [InlineData("{\"Authentication\":{\"ProtectedSigningKey\":\"first\",\"protectedSigningKey\":\"second\"}}")]
    [InlineData("{\"Authentication\":{\"SigningKeyProtection\":{\"Provider\":\"Auto\",\"provider\":\"FileSystem\"}}}")]
    [InlineData("{\"Authentication\":{\"SigningKeyProtection\":{\"Provider\":\"SystemdCreds\"}}}")]
    [InlineData("{\"Authentication\":{\"ProtectedSigningKey\":null}}")]
    [InlineData("{\"Authentication\":{\"ProtectedSigningKey\":\"\"}}")]
    [InlineData("{\"Authentication\":{\"ProtectedSigningKey\":\"   \"}}")]
    [InlineData("{\"Authentication\":{\"ProtectedSigningKey\":{\"Unexpected\":true}}}")]
    [InlineData("not json")]
    public async Task InvalidConfigurationFailsWithoutCreatingKeyRing(string json)
    {
        await InitializeConfigurationAsync(json);
        await Assert.ThrowsAnyAsync<Exception>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Equal(json, await File.ReadAllTextAsync(ConfigPath));
        Assert.False(Directory.Exists(KeyRingPath));
    }

    [Fact]
    public async Task InitializationPreservesUnknownSettingsAndAcceptsCommentedCaseInsensitiveConfiguration()
    {
        await InitializeConfigurationAsync("""
            {
                // These settings must survive initializing the protected key.
                "CustomRoot": { "items": [1, "two", null] },
                "authentication": {
                    "issuer": "custom-issuer",
                    "TokenLifetimeMinutes": 15,
                    "StaticClients": [{ "ClientId": "existing-client" }],
                    "SigningKeyProtection": { "Provider": "FileSystem", "CustomProtection": true },
                },
            }
            """);

        var signingKey = await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        var written = await File.ReadAllTextAsync(ConfigPath);
        var root = JsonNode.Parse(written)!;
        var authentication = root["Authentication"]!;
        Assert.Equal("[1,\"two\",null]", root["CustomRoot"]!["items"]!.ToJsonString());
        Assert.Equal("custom-issuer", authentication["Issuer"]!.GetValue<string>());
        Assert.Equal(15, authentication["TokenLifetimeMinutes"]!.GetValue<int>());
        Assert.Equal("existing-client", authentication["StaticClients"]![0]!["ClientId"]!.GetValue<string>());
        Assert.True(authentication["SigningKeyProtection"]!["CustomProtection"]!.GetValue<bool>());
        Assert.Null(authentication["SigningKey"]);
        Assert.DoesNotContain(signingKey, written);
        Assert.Equal(signingKey, await ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigPath)!, "*.tmp"));
    }

    [Fact]
    public async Task InitializationPreservesExplicitDefaultsAndNullSettings()
    {
        await InitializeConfigurationAsync("""
            {
                "Authentication": {
                    "TokenLifetimeMinutes": 60,
                    "RefreshTokenLifetimeDays": 7,
                    "Issuer": "GenetecSecurityCenter",
                    "Audience": null,
                    "SigningKeyProtection": { "Provider": "Auto", "KeyRingPath": null }
                }
            }
            """);

        var signingKey = await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(ConfigPath));
        var authentication = written.RootElement.GetProperty(nameof(SigningKeyConfiguration.Authentication));

        Assert.Equal(60, authentication.GetProperty(nameof(AuthenticationOptions.TokenLifetimeMinutes)).GetInt32());
        Assert.Equal(7, authentication.GetProperty(nameof(AuthenticationOptions.RefreshTokenLifetimeDays)).GetInt32());
        Assert.Equal("GenetecSecurityCenter", authentication.GetProperty(nameof(AuthenticationOptions.Issuer)).GetString());
        Assert.Equal(JsonValueKind.Null, authentication.GetProperty(nameof(AuthenticationOptions.Audience)).ValueKind);
        var protection = authentication.GetProperty(nameof(AuthenticationOptions.SigningKeyProtection));
        Assert.Equal("Auto", protection.GetProperty(nameof(SigningKeyProtectionSettings.Provider)).GetString());
        Assert.Equal(JsonValueKind.Null, protection.GetProperty(nameof(SigningKeyProtectionSettings.KeyRingPath)).ValueKind);
        Assert.Equal(signingKey, await ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedConfigurationWritePreservesOriginalFileAndLeavesNoStagedFile(bool canceled)
    {
        await InitializeConfigurationAsync();
        var original = await File.ReadAllTextAsync(ConfigPath);
        var root = await SigningKeyConfigurationFile.ReadAsync(ConfigPath, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        if (canceled)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SigningKeyConfigurationFile.WriteAsync(ConfigPath, root, cancellation.Token));
        }
        else
        {
            root.AdditionalSettings["LargeSetting"] = JsonSerializer.SerializeToElement(new string('x', SigningKeyConfigurationFile.MaximumBytes));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                SigningKeyConfigurationFile.WriteAsync(ConfigPath, root, cancellation.Token));
        }

        Assert.Equal(original, await File.ReadAllTextAsync(ConfigPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigPath)!, "*.tmp"));
    }

    [Fact]
    public async Task CorruptedCiphertextCannotTriggerReplacement()
    {
        await InitializeConfigurationAsync();
        await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(ConfigPath))!;
        var authentication = root["Authentication"]!.AsObject();
        var ciphertext = WebEncoders.Base64UrlDecode(authentication["ProtectedSigningKey"]!.GetValue<string>());
        ciphertext[^1] ^= 1;
        authentication["ProtectedSigningKey"] = WebEncoders.Base64UrlEncode(ciphertext);
        var damaged = root.ToJsonString();
        await File.WriteAllTextAsync(ConfigPath, damaged);
        var keyFiles = Directory.GetFiles(KeyRingPath);
        await Assert.ThrowsAnyAsync<CryptographicException>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Equal(damaged, await File.ReadAllTextAsync(ConfigPath));
        Assert.Equal(keyFiles, Directory.GetFiles(KeyRingPath));
    }

    [Fact]
    public async Task MissingKeyFileFailsWithoutGeneratingReplacement()
    {
        await InitializeConfigurationAsync();
        await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        var written = await File.ReadAllTextAsync(ConfigPath);
        File.Delete(Assert.Single(Directory.GetFiles(KeyRingPath)));
        await Assert.ThrowsAsync<CryptographicException>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Empty(Directory.GetFiles(KeyRingPath));
        Assert.Equal(written, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task CorruptedKeyFileFailsWithoutGeneratingReplacement()
    {
        await InitializeConfigurationAsync();
        await ProtectedSigningKeyStore.LoadOrCreateAsync(Options);
        var written = await File.ReadAllTextAsync(ConfigPath);
        var keyFile = Assert.Single(Directory.GetFiles(KeyRingPath));
        await File.WriteAllTextAsync(keyFile, "<invalid");

        await Assert.ThrowsAnyAsync<CryptographicException>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Equal(keyFile, Assert.Single(Directory.GetFiles(KeyRingPath)));
        Assert.Equal("<invalid", await File.ReadAllTextAsync(keyFile));
        Assert.Equal(written, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task MissingConfigurationAndCancellationDoNotCreateKeys()
    {
        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options));
        Assert.Equal(ConfigPath, error.FileName);
        Assert.Contains(ConfigPath, error.Message);
        Assert.False(Directory.Exists(KeyRingPath));
        await InitializeConfigurationAsync();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProtectedSigningKeyStore.LoadOrCreateAsync(Options, canceled.Token));
        Assert.False(Directory.Exists(KeyRingPath));
    }

    [Fact]
    public async Task UnsupportedProviderAndRelativeKeyRingPathFailWithoutCreatingState()
    {
        await InitializeConfigurationAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ProtectedSigningKeyStore.LoadOrCreateAsync(Options with { Provider = (SigningKeyProtectionProvider)3 }));
        if (!OperatingSystem.IsWindows())
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
                ProtectedSigningKeyStore.LoadOrCreateAsync(Options with { Provider = SigningKeyProtectionProvider.WindowsDpapi }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProtectedSigningKeyStore.LoadOrCreateAsync(Options with { KeyRingPath = "relative-keys" }));
        Assert.False(Directory.Exists(KeyRingPath));
    }

    [Fact]
    public void DefaultKeyRingPathUsesPersistentPlatformLocation()
    {
        var path = ProtectedSigningKeyStore.GetDefaultKeyRingPath(ConfigPath);
        Assert.True(Path.IsPathFullyQualified(path));
        if (OperatingSystem.IsLinux())
        {
            var expected = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
                ? "/app/config/keys"
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "genetec", "mcp", "keys");
            Assert.Equal(expected, path);
        }
        else
            Assert.Equal(Path.Combine(Path.GetDirectoryName(ConfigPath)!, "keys"), path);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
