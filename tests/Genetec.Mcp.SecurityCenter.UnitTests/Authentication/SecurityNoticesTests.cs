using System.Net;
using Genetec.Mcp.SecurityCenter.Authentication;
using Genetec.Mcp.SecurityCenter.Configuration;
using Genetec.Mcp.SecurityCenter.UnitTests.Audit;
using Microsoft.Extensions.Logging;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public sealed class SecurityNoticesTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void StartupAndSignInUseSameNoticesForEffectiveSettings(
        bool useHttps, bool skipCertificateValidation, bool stdio)
    {
        var options = new ConnectionOptions
        {
            UseHttps = useHttps,
            SkipCertificateValidation = skipCertificateValidation
        };
        var logger = new CapturingLogger();
        SecurityNotices.LogStartup(logger, options);
        var page = WebUtility.HtmlDecode(AuthorizeEndpoint.BuildLoginPage("", "", "", "", "", "",
            connectionOptions: options, showBrowserHttpWarning: !stdio));

        Assert.Contains($"<h3 id=\"agentNoticeTitle\">{SecurityNotices.AgentAccessTitle}</h3>", page);
        Assert.Contains($"<p>{SecurityNotices.AgentAccessMessage}</p>", page);
        Assert.Contains(logger.Entries, entry => entry.Message ==
            $"{SecurityNotices.AgentAccessTitle}: {SecurityNotices.AgentAccessMessage}");
        Assert.Equal(!useHttps, page.Contains(SecurityNotices.WebSdkHttpMessage));
        Assert.Equal(!useHttps, logger.Entries.Any(entry => entry.Message ==
            $"{SecurityNotices.WebSdkHttpTitle}: {SecurityNotices.WebSdkHttpMessage}"));
        Assert.Equal(useHttps && skipCertificateValidation, page.Contains(SecurityNotices.CertificateValidationMessage));
        Assert.Equal(useHttps && skipCertificateValidation, logger.Entries.Any(entry => entry.Message ==
            $"{SecurityNotices.CertificateValidationTitle}: {SecurityNotices.CertificateValidationMessage}"));
        Assert.Equal(!stdio, page.Contains(SecurityNotices.BrowserHttpMessage));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains(SecurityNotices.BrowserHttpMessage));
        Assert.Equal(useHttps && !skipCertificateValidation ? 1 : 2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.DoesNotContain("{{", page);
    }

    [Fact]
    public void MissingWebSdkSettingsStillLogsAgentDisclaimer()
    {
        var logger = new CapturingLogger();
        SecurityNotices.LogStartup(logger, null);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"{SecurityNotices.AgentAccessTitle}: {SecurityNotices.AgentAccessMessage}", entry.Message);
    }
}
