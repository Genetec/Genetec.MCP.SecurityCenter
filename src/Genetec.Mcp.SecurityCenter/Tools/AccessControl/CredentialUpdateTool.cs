using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class CredentialUpdateTool
{
    [McpServerTool(
        Name = "credential_update",
        Title = "Update Credential",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more credentials in Security Center. " +
        "Provide a single item to rename one credential, or multiple items for a batch rename.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Credential update request")] CredentialUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "Credential");
            return await client.UpdateEntity(item.Id.ToString(), new { Name = EntityNameSanitizer.Sanitize(item.Name) });
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)new { Name = EntityNameSanitizer.Sanitize(i.Name) }))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "Credential");
    }
}

public sealed record CredentialUpdateRequest(
    [property: Description("One or more credentials to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CredentialUpdateItem> Items);

public sealed record CredentialUpdateItem(
    [property: Description("Credential GUID"), Required] Guid Id,
    [property: Description("New credential name"), Required, MinLength(1)] string Name);
