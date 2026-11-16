using System.ComponentModel;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class SystemConfigurationTool
{
    private const string SystemConfigurationGuid = "00000000-0000-0000-0000-000000000007";

    [McpServerTool(
        Name = "system_configuration",
        Title = "System Configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Get the Security Center system configuration including system name, system ID, system version, license details, license count, license expiration, and other global system settings")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntity(SystemConfigurationGuid);
    }
}
