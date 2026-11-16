using Genetec.Mcp.SecurityCenter.Configuration;

namespace Genetec.Mcp.SecurityCenter.Authentication;

internal static class SecurityNotices
{
    internal const string AgentAccessTitle = "AI agent access";
    internal const string AgentAccessMessage = "Agents can make mistakes. Grant only needed access and review sensitive actions. You are responsible for supervising your agent; Genetec disclaims liability for its actions to the extent permitted by law and your agreement.";
    internal const string BrowserHttpTitle = "Sign-in connection is not secure";
    internal const string BrowserHttpMessage = "Your credentials could be exposed. Use HTTPS before signing in.";
    internal const string WebSdkHttpTitle = "Security Center connection is not secure";
    internal const string WebSdkHttpMessage = "Your credentials and data could be exposed. Enable HTTPS for Security Center.";
    internal const string CertificateValidationTitle = "Security Center identity is not verified";
    internal const string CertificateValidationMessage = "Your credentials and data could be intercepted. Enable certificate validation.";

    internal static void LogStartup(ILogger logger, ConnectionOptions? connectionOptions)
    {
        logger.LogWarning("{Title}: {Message}", AgentAccessTitle, AgentAccessMessage);
        if (connectionOptions?.UseHttps == false)
            logger.LogWarning("{Title}: {Message}", WebSdkHttpTitle, WebSdkHttpMessage);
        if (connectionOptions is { UseHttps: true, SkipCertificateValidation: true })
            logger.LogWarning("{Title}: {Message}", CertificateValidationTitle, CertificateValidationMessage);
    }
}
