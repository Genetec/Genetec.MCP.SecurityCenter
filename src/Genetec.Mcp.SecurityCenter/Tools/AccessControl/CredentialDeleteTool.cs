using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CredentialDeleteTool
{
    [McpServerTool(
        Name = "credential_delete",
        Title = "Delete Credential",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Permanently delete one or more credentials from Security Center. " +
        "Provide a single GUID or multiple GUIDs for batch deletion.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential delete request")] CredentialDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "Credential");
    }
}

public sealed record CredentialDeleteRequest(
    [property: Description("One or more credential GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
