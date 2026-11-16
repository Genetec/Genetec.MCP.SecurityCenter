using System.ComponentModel;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AssuranceProfileListTool
{
    [McpServerTool(
        Name = "assurance_profile_list",
        Title = "List Assurance Profiles",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "List all configured credential assurance profiles, including ProfileId, Description, and Mode. " +
        "Use ProfileId as assurance_profile_id for reader_configuration.enable_assurance_profile. " +
        "Assurance profiles are system configuration records, not entities.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetAssuranceProfiles();
    }
}
