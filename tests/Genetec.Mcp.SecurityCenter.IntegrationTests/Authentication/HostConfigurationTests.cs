using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class HostConfigurationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"mcp-host-configuration-{Guid.NewGuid():N}");
    private readonly string environmentKey = $"McpConfigurationTest_{Guid.NewGuid():N}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActiveJsonEnvironmentAndCliOverridesPreserveHostDefaults(bool http)
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "appsettings.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            Authentication = new { Issuer = "active-file", SigningKeyProtection = new { Provider = "FileSystem", KeyRingPath = Path.Combine(directory, "keys") } },
            Transport = new { Stdio = new { Enabled = true }, Http = new { Enabled = false } },
            EnvironmentSettings = new Dictionary<string, string> { [environmentKey] = "file" }
        }));
        Environment.SetEnvironmentVariable($"EnvironmentSettings__{environmentKey}", "environment");

        var configuration = http ? WebApplication.CreateBuilder().Configuration
            : Host.CreateApplicationBuilder().Configuration;
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["PreservedHostDefault"] = "present" });
        configuration.AddSecurityCenterConfiguration(configPath, CliOptions.Parse(["--transport", "http"]));

        Assert.Equal("present", configuration["PreservedHostDefault"]);
        Assert.Equal("environment", configuration[$"EnvironmentSettings:{environmentKey}"]);
        Assert.False(configuration.GetValue<bool>("Transport:Stdio:Enabled"));
        Assert.True(configuration.GetValue<bool>("Transport:Http:Enabled"));
        Assert.Equal("active-file", configuration[$"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.Issuer)}"]);
        var signingKey = await HttpSigningKeyStartup.LoadAsync(configuration, configPath);
        Assert.Equal(signingKey, await HttpSigningKeyStartup.LoadAsync(configuration, configPath));
        Assert.DoesNotContain(signingKey, await File.ReadAllTextAsync(configPath));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Authentication\":{}}")]
    [InlineData("{\"Authentication\":{\"SigningKeyProtection\":{}}}")]
    public async Task InitializationAndRestartPreserveInheritedAuthenticationSettings(string json)
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "appsettings.json");
        var keyRingPath = Path.Combine(directory, "inherited-keys");
        await File.WriteAllTextAsync(configPath, json);

        ConfigurationManager CreateConfiguration()
        {
            var configuration = WebApplication.CreateBuilder().Configuration;
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Issuer"] = "host-issuer",
                ["Authentication:Audience"] = "host-audience",
                ["Authentication:TokenLifetimeMinutes"] = "15",
                ["Authentication:RefreshTokenLifetimeDays"] = "3",
                ["Authentication:SigningKeyProtection:Provider"] = "FileSystem",
                ["Authentication:SigningKeyProtection:KeyRingPath"] = keyRingPath
            });
            configuration.AddSecurityCenterConfiguration(configPath, CliOptions.Parse(["--transport", "http"]));
            return configuration;
        }

        using var initial = CreateConfiguration();
        var signingKey = await HttpSigningKeyStartup.LoadAsync(initial, configPath);
        using var restarted = CreateConfiguration();
        var authentication = restarted.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>()!;

        Assert.Equal("host-issuer", authentication.Issuer);
        Assert.Equal("host-audience", authentication.Audience);
        Assert.Equal(15, authentication.TokenLifetimeMinutes);
        Assert.Equal(3, authentication.RefreshTokenLifetimeDays);
        Assert.Equal(SigningKeyProtectionProvider.FileSystem, authentication.SigningKeyProtection.Provider);
        Assert.Equal(keyRingPath, authentication.SigningKeyProtection.KeyRingPath);
        Assert.Equal(signingKey, await HttpSigningKeyStartup.LoadAsync(restarted, configPath));
    }

    [Fact]
    public async Task QuotedAuthenticationLifetimesRemainValidAcrossStartupAndRestart()
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "appsettings.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            Authentication = new
            {
                TokenLifetimeMinutes = "15",
                RefreshTokenLifetimeDays = "3",
                SigningKeyProtection = new { Provider = "FileSystem", KeyRingPath = Path.Combine(directory, "keys") }
            }
        }));

        using var initial = new ConfigurationManager();
        initial.AddSecurityCenterConfiguration(configPath, CliOptions.Parse([]));
        var signingKey = await HttpSigningKeyStartup.LoadAsync(initial, configPath);
        using var restarted = new ConfigurationManager();
        restarted.AddSecurityCenterConfiguration(configPath, CliOptions.Parse([]));
        var authentication = restarted.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>()!;

        Assert.Equal(15, authentication.TokenLifetimeMinutes);
        Assert.Equal(3, authentication.RefreshTokenLifetimeDays);
        Assert.Equal(signingKey, await HttpSigningKeyStartup.LoadAsync(restarted, configPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plaintext")]
    public async Task PlaintextInRetainedHostProviderIsRejectedBeforeCreatingState(string? value)
    {
        var configuration = WebApplication.CreateBuilder().Configuration;
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.SigningKey)}"] = value
        });
        configuration.AddSecurityCenterConfiguration(Path.Combine(directory, "missing.json"), CliOptions.Parse([]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HttpSigningKeyStartup.LoadAsync(configuration, Path.Combine(directory, "missing.json")));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("SystemdCreds")]
    [InlineData("3")]
    public async Task RetiredProviderFromHostConfigurationFailsWithoutCreatingKeys(string provider)
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "appsettings.json");
        var keyRingPath = Path.Combine(directory, "keys");
        await File.WriteAllTextAsync(configPath, "{}");
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKeyProtection:Provider"] = provider,
            ["Authentication:SigningKeyProtection:KeyRingPath"] = keyRingPath
        });

        await Assert.ThrowsAnyAsync<Exception>(() => HttpSigningKeyStartup.LoadAsync(configuration, configPath));
        Assert.False(Directory.Exists(keyRingPath));
        Assert.Equal("{}", await File.ReadAllTextAsync(configPath));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable($"EnvironmentSettings__{environmentKey}", null);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
