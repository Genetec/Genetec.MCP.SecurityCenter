namespace Genetec.Mcp.SecurityCenter.Authentication;

public sealed class ScConnectionContext
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 4590;
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public bool UseHttps { get; init; } = true;
    public bool SkipCertificateValidation { get; init; }
    public int InvokeTimeoutSec { get; init; } = 30;

}
