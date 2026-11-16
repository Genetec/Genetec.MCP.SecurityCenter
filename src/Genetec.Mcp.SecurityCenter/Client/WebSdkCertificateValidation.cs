using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Genetec.Mcp.SecurityCenter.Client;

internal static class WebSdkCertificateValidation
{
    internal static bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        var chainStatus = chain?.ChainStatus.Aggregate(X509ChainStatusFlags.NoError,
            (flags, status) => flags | status.Status) ?? X509ChainStatusFlags.NoError;
        var leaf = certificate as X509Certificate2;
        // Carry the reason on this connection's exception, never on a shared handler.
        throw new WebSdkCertificateValidationException(errors, chainStatus, leaf?.NotBefore, leaf?.NotAfter);
    }
}

internal sealed class WebSdkCertificateValidationException(
    SslPolicyErrors errors, X509ChainStatusFlags chainStatus, DateTime? notBefore = null, DateTime? notAfter = null)
    : AuthenticationException(BuildMessage(errors, chainStatus, notBefore, notAfter))
{
    private static string BuildMessage(SslPolicyErrors errors, X509ChainStatusFlags chainStatus, DateTime? notBefore, DateTime? notAfter)
    {
        var reasons = new List<string>();
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            reasons.Add("the server did not provide a certificate");
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            reasons.Add("the certificate does not match the Security Center hostname");
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            if (chainStatus.HasFlag(X509ChainStatusFlags.NotTimeValid))
            {
                var now = DateTime.UtcNow;
                reasons.Add(notBefore?.ToUniversalTime() > now ? "the certificate is not yet valid"
                    : notAfter?.ToUniversalTime() < now ? "the certificate has expired"
                    : "the certificate chain contains a certificate outside its validity period");
            }
            if ((chainStatus & (X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain)) != 0)
                reasons.Add("the certificate issuer is not trusted or its certificate chain is incomplete");
            if (chainStatus.HasFlag(X509ChainStatusFlags.Revoked))
                reasons.Add("a certificate in the chain has been revoked");
            var classified = X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.UntrustedRoot
                | X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.Revoked;
            if (chainStatus == X509ChainStatusFlags.NoError || (chainStatus & ~classified) != 0)
                reasons.Add("the certificate chain failed validation");
        }

        return $"Security Center Web SDK certificate validation failed: {string.Join("; ", reasons)}. "
            + "Ask your administrator to correct the certificate or trust configuration.";
    }
}
