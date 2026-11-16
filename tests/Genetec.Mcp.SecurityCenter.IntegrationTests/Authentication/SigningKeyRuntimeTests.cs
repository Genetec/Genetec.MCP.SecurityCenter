using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public sealed class SigningKeyRuntimeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RefreshTokenIssuance_RequiresOAuthClientBindingBeforeWritingResponse(string? oauthClientId)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var options = new AuthenticationOptions { SigningKey = new string('k', 64) };

        await Assert.ThrowsAsync<ArgumentException>(() => TokenEndpoint.WriteTokenResponse(
            context, options, "sc-user", "utilities", includeRefreshToken: true, oauthClientId: oauthClientId));

        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task ExplicitRuntimeKeyDrivesOptionsJwtTokensAndSessionContext()
    {
        var signingKey = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "a plaintext configuration value must not win",
            ["Authentication:Issuer"] = "test-issuer",
            ["Authentication:Audience"] = "test-audience"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecurityCenterAuthentication(configuration, signingKey);
        using var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(signingKey, authOptions.SigningKey);

        var bearerOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var runtimeKeyBytes = Assert.IsType<SymmetricSecurityKey>(
            bearerOptions.TokenValidationParameters.IssuerSigningKey).Key;
        Assert.Equal(Encoding.UTF8.GetBytes(signingKey), runtimeKeyBytes);
        Assert.False(Convert.FromBase64String(signingKey).AsSpan().SequenceEqual(runtimeKeyBytes));

        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await TokenEndpoint.WriteTokenResponse(context, authOptions, "mcp-client", "", includeRefreshToken: true,
            oauthClientId: "oauth-client", clientAuthentication: new TokenEndpoint.RefreshClientAuthentication("none"));
        body.Position = 0;
        using var response = await JsonDocument.ParseAsync(body);
        var accessToken = response.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = response.RootElement.GetProperty("refresh_token").GetString()!;
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = bearerOptions.TokenValidationParameters.IssuerSigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        Assert.Equal("mcp-client", tokenHandler.ValidateToken(accessToken, validation, out _)
            .FindFirstValue(JwtRegisteredClaimNames.Sub));
        var refreshPrincipal = tokenHandler.ValidateToken(refreshToken, validation, out _);
        Assert.Equal("refresh", refreshPrincipal.FindFirstValue("token_type"));
        Assert.Equal("mcp-client", refreshPrincipal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("oauth-client", refreshPrincipal.FindFirstValue("oauth_client_id"));

        var protector = provider.GetRequiredService<SessionContextProtector>();
        var connection = new ScConnectionContext { Host = "sc.test", User = "user", Password = "secret" };
        var protectedContext = protector.Protect(connection);
        Assert.Equal(connection.Host, protector.Unprotect(protectedContext)!.Host);
        Assert.Equal(connection.Password, protector.Unprotect(protectedContext)!.Password);
    }

    [Theory]
    [InlineData("must never be used")]
    [InlineData("")]
    [InlineData(null)]
    public async Task HttpStartupRejectsPlaintextSigningKeyPresenceFromAnyConfigurationProvider(string? configuredValue)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = configuredValue
        }).Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HttpSigningKeyStartup.LoadAsync(configuration, Path.Combine(Path.GetTempPath(), "missing-config.json")));

        Assert.Contains("cannot be supplied", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("must never be used", error.Message);
    }

    [Fact]
    public async Task DataProtectionStartupReusesKeyAndRejectsAuthenticationFromAnotherDeployment()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mcp-signing-key-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configPath = Path.Combine(directory, "appsettings.json");
            var keyRingPath = Path.Combine(directory, "keys");
            await File.WriteAllTextAsync(configPath,
                "{\"Authentication\":{\"Issuer\":\"startup-test\"}} ");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKeyProtection:Provider"] = "FileSystem",
                ["Authentication:SigningKeyProtection:KeyRingPath"] = keyRingPath
            }).Build();

            var first = await HttpSigningKeyStartup.LoadAsync(configuration, configPath);
            var second = await HttpSigningKeyStartup.LoadAsync(configuration, configPath);

            Assert.Equal(first, second);
            Assert.Equal(32, Convert.FromBase64String(first).Length);
            Assert.True(File.Exists(configPath));
            Assert.NotEmpty(Directory.GetFiles(keyRingPath, "key-*.xml"));
            Assert.Contains("startup-test", await File.ReadAllTextAsync(configPath));
            Assert.DoesNotContain(first, await File.ReadAllTextAsync(configPath));

            var originalAuth = new AuthenticationOptions
            {
                SigningKey = first,
                Issuer = "data-protection-test",
                Audience = "data-protection-test"
            };
            var connection = new ScConnectionContext { Host = "sc.test", User = "user", Password = "password" };
            string protectedContext;
            using (var originalProtector = new SessionContextProtector(Options.Create(originalAuth)))
                protectedContext = originalProtector.Protect(connection);

            var tokenContext = new DefaultHttpContext();
            await using var tokenBody = new MemoryStream();
            tokenContext.Response.Body = tokenBody;
            await TokenEndpoint.WriteTokenResponse(tokenContext, originalAuth, "mcp-client", "", includeRefreshToken: false);
            tokenBody.Position = 0;
            using var tokenResponse = await JsonDocument.ParseAsync(tokenBody);
            var oldAccessToken = tokenResponse.RootElement.GetProperty("access_token").GetString()!;
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var validated = tokenHandler.ValidateToken(oldAccessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = originalAuth.Issuer,
                ValidateAudience = true,
                ValidAudience = originalAuth.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(second)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            Assert.Equal("mcp-client", validated.FindFirstValue(JwtRegisteredClaimNames.Sub));

            var restartedAuth = new AuthenticationOptions
            {
                SigningKey = second,
                Issuer = originalAuth.Issuer,
                Audience = originalAuth.Audience
            };
            using var restartedProtector = new SessionContextProtector(Options.Create(restartedAuth));
            var recovered = restartedProtector.Unprotect(protectedContext);
            Assert.NotNull(recovered);
            Assert.Equal(connection.Host, recovered!.Host);
            Assert.Equal(connection.Password, recovered!.Password);

            // A different deployment uses a separate configuration and key ring.
            var replacementDirectory = Path.Combine(directory, "replacement");
            Directory.CreateDirectory(replacementDirectory);
            var replacementConfigPath = Path.Combine(replacementDirectory, "appsettings.json");
            await File.WriteAllTextAsync(replacementConfigPath, "{\"Authentication\":{\"Issuer\":\"startup-test\"}}");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(replacementConfigPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var replacementConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKeyProtection:Provider"] = "FileSystem",
                ["Authentication:SigningKeyProtection:KeyRingPath"] =
                    Path.Combine(replacementDirectory, "keys")
            }).Build();
            var replacementKey = await HttpSigningKeyStartup.LoadAsync(replacementConfiguration, replacementConfigPath);
            Assert.NotEqual(first, replacementKey);

            var replacementAuth = new AuthenticationOptions
            {
                SigningKey = replacementKey,
                Issuer = originalAuth.Issuer,
                Audience = originalAuth.Audience
            };
            Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => tokenHandler.ValidateToken(
                oldAccessToken, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = replacementAuth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = replacementAuth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(replacementKey)),
                    ValidateLifetime = true
                }, out _));
            using var replacementProtector = new SessionContextProtector(Options.Create(replacementAuth));
            Assert.Null(replacementProtector.Unprotect(protectedContext));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PlaintextSigningKeyValidatorRejectsEvenWhenAuthenticationOptionsCouldOtherwiseBeBound()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "configured-secret"
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            HttpSigningKeyStartup.RejectPlaintextSigningKey(configuration));

        Assert.DoesNotContain("configured-secret", error.Message);
    }

}
