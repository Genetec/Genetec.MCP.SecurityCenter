using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Configuration;

public class SecurityCenterOptionsTests
{
    [Fact]
    public void ShippedConfiguration_EnablesCertificateValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();
        Assert.False(configuration.GetValue<bool>("SecurityCenter:WebSdkSettings:SkipCertificateValidation"));
        Assert.False(new ConnectionOptions().SkipCertificateValidation);
        Assert.Null(configuration["SecurityCenter:WebSdkSettings:ApplicationId"]);
    }

    [Theory]
    [InlineData("browser-password")]
    [InlineData("")]
    public void RuntimeConfiguration_KeepsAllowlistAndUsesBrowserCredentials(string browserPassword)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecurityCenter:WebSdkSettings:Hosts:0"] = "allowed.example.com",
            ["SecurityCenter:WebSdkSettings:Port"] = "4590",
            ["SecurityCenter:WebSdkSettings:ApplicationId"] = "test-app",
            // A legacy singular Host key must not override the allowlist.
            ["SecurityCenter:WebSdkSettings:Host"] = "test-only.example.com",
            // The shared type binds credentials for tests, but runtime sign-in must ignore them.
            ["SecurityCenter:WebSdkSettings:User"] = "configured-user",
            ["SecurityCenter:WebSdkSettings:Password"] = "configured-password"
        }).Build();
        var options = configuration.GetSection(SecurityCenterOptions.SectionName).Get<SecurityCenterOptions>()!;
        Assert.Equal(["allowed.example.com"], options.WebSdkSettings!.Hosts);
        Assert.Equal("configured-user", options.WebSdkSettings.User);
        Assert.Equal("configured-password", options.WebSdkSettings.Password);

        var services = new ServiceCollection();
        services.AddHttpClient(SecurityCenterSessionManager.HttpClientName);
        using var provider = services.BuildServiceProvider();
        var manager = new SecurityCenterSessionManager(
            Options.Create(options), provider.GetRequiredService<IHttpClientFactory>(), provider);

        var connection = manager.CreateConnectionContext("browser-user", browserPassword, "allowed.example.com");

        Assert.Equal("allowed.example.com", connection.Host);
        Assert.Equal("browser-user", connection.User);
        Assert.Equal(browserPassword, connection.Password);
        Assert.Equal(4590, connection.Port);
        var session = new SecurityCenterSession(connection);
        var credentials = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(session.AuthHeader["Basic ".Length..]));
        Assert.Equal($"browser-user;{SecurityCenterSession.ApplicationId}:{browserPassword}", credentials);
        Assert.Null(typeof(ConnectionOptions).GetProperty("ApplicationId"));
        Assert.Null(typeof(ScConnectionContext).GetProperty("ApplicationId"));
        Assert.Throws<ArgumentException>(() => manager.CreateConnectionContext("browser-user", browserPassword, ""));
    }
}
