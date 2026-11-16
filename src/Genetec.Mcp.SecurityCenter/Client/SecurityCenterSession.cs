using System.Text;
using Genetec.Mcp.SecurityCenter.Authentication;

namespace Genetec.Mcp.SecurityCenter.Client;

public class SecurityCenterSession
{
    internal const string ApplicationId = "r1Atkre/if8To82Ca8Igx7az1NPadawtMeOgkKONy/8LpF3N5qsZN+SJ4kdUKPt9";

    public string BaseUrl { get; }
    public string AuthHeader { get; }
    public int InvokeTimeoutSec { get; }
    public SecurityCenterSession(ScConnectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Host))
            throw new ArgumentException("A selected Security Center host is required.", nameof(context));
        var scheme = context.UseHttps ? "https" : "http";
        var selectedHost = context.Host.Trim();
        var host = selectedHost.Contains(':') && !selectedHost.StartsWith('[')
            ? $"[{selectedHost}]"
            : selectedHost;
        BaseUrl = $"{scheme}://{host}:{context.Port}/WebSdk/";
        InvokeTimeoutSec = context.InvokeTimeoutSec;

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{context.User};{ApplicationId}:{context.Password}"));
        AuthHeader = $"Basic {credentials}";
    }
}
