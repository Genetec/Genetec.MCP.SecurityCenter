using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Authentication;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Client;

public class SecurityCenterSessionTests
{
    [Theory]
    [InlineData("::1")]
    [InlineData("[::1]")]
    public void Constructor_Ipv6Host_BuildsValidBaseUrl(string host)
    {
        var session = new SecurityCenterSession(new ScConnectionContext { Host = host, Port = 4590 });

        Assert.Equal("https://[::1]:4590/WebSdk/", session.BaseUrl);
    }

    [Fact]
    public void Constructor_BuildsHttpsBaseUrl()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "192.168.1.100",
            Port = 4590
        });

        Assert.Equal("https://192.168.1.100:4590/WebSdk/", session.BaseUrl);
    }

    [Fact]
    public void Constructor_CustomPort_ReflectedInBaseUrl()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "10.0.0.1",
            Port = 8443
        });

        Assert.Equal("https://10.0.0.1:8443/WebSdk/", session.BaseUrl);
    }

    // SkipCertificateValidation affects the HttpClient handler, not the scheme —
    // BaseUrl is always HTTPS regardless.
    [Fact]
    public void Constructor_SkipCertValidation_StillUsesHttps()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "10.0.0.1",
            Port = 4590,
            SkipCertificateValidation = true
        });

        Assert.Equal("https://10.0.0.1:4590/WebSdk/", session.BaseUrl);
    }

    [Fact]
    public void Constructor_BuildsCorrectAuthHeader()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost",
            User = "admin",
            Password = "password",
        });

        var expected = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                "admin;r1Atkre/if8To82Ca8Igx7az1NPadawtMeOgkKONy/8LpF3N5qsZN+SJ4kdUKPt9:password"));
        Assert.Equal($"Basic {expected}", session.AuthHeader);
    }

    [Theory]
    [InlineData("custom-app")]
    [InlineData("")]
    public void Constructor_RestoredContextCannotOverrideApplicationId(string applicationId)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Host = "localhost",
            User = "admin",
            Password = "password",
            ApplicationId = applicationId
        });
        var context = System.Text.Json.JsonSerializer.Deserialize<ScConnectionContext>(json)!;

        var session = new SecurityCenterSession(context);

        var credentials = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(session.AuthHeader["Basic ".Length..]));
        Assert.Equal($"admin;{SecurityCenterSession.ApplicationId}:password", credentials);
    }

    [Fact]
    public void Constructor_NullPassword_IncludedAsEmpty()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost",
            User = "admin",
            Password = "",
        });

        Assert.StartsWith("Basic ", session.AuthHeader);
        Assert.False(string.IsNullOrEmpty(session.AuthHeader));
    }

    [Fact]
    public void Constructor_InvokeTimeoutSec_Reflected()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "localhost",
            InvokeTimeoutSec = 60
        });

        Assert.Equal(60, session.InvokeTimeoutSec);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingFirstHost_Throws(string? host)
    {
        var context = new ScConnectionContext { Host = host! };

        Assert.Throws<ArgumentException>(() => new SecurityCenterSession(context));
    }

    [Fact]
    public void Constructor_UsesTheSelectedContextHost()
    {
        var session = new SecurityCenterSession(new ScConnectionContext
        {
            Host = "second.example.com"
        });

        Assert.Equal("https://second.example.com:4590/WebSdk/", session.BaseUrl);
    }
}
