namespace Genetec.Mcp.SecurityCenter.Configuration;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public int McpMaxConcurrentPerUser { get; set; } = 5;
    public int McpConcurrencyWaitSeconds { get; set; } = 30;
    public int McpWindowCallLimit { get; set; } = 200;
    public int McpWindowMinutes { get; set; } = 5;
    public int AuthorizeWindowAttemptLimit { get; set; } = 10;
    public int AuthorizeWindowSeconds { get; set; } = 60;
    public int TokenWindowAttemptLimit { get; set; } = 30;
    public int TokenWindowSeconds { get; set; } = 60;
}

public class SecurityCenterOptions
{
    public const string SectionName = "SecurityCenter";
    public ConnectionOptions? WebSdkSettings { get; set; }
}

public class ConnectionOptions
{
    public string[] Hosts { get; set; } = [];
    public int Port { get; set; } = 4590;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseHttps { get; set; } = true;
    public bool SkipCertificateValidation { get; set; } = false;
    public int InvokeTimeoutSec { get; set; } = 30;
}

public class TransportOptions
{
    public const string SectionName = "Transport";
    // Tool scopes preselected in either transport's editable sign-in form. Empty = all.
    public ToolScope[] Scopes { get; set; } = [];
    public StdioTransportOptions Stdio { get; set; } = new();
    public HttpTransportOptions Http { get; set; } = new();
}

public class StdioTransportOptions
{
    public bool Enabled { get; set; } = true;
}

public class HttpTransportOptions
{
    public bool Enabled { get; set; } = false;
}
