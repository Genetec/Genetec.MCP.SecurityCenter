using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class RoleCreateTool
{
    [McpServerTool(Name = "role_create", Title = "Create Role", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true),
     Description("Create one or more roles in Security Center with optional type and server assignment. Provide a single item for a direct create, or multiple items for a batch create.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Role create request")] RoleCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            return await client.CreateEntity<EntityCreateResult>("Role", EntityNameSanitizer.Sanitize(item.Name),
                new { Type = item.RoleType?.ToString() });
        }
        var batchItems = request.Items
            .Select(item => new SecurityCenterRestClient.BatchCreateItem(EntityNameSanitizer.Sanitize(item.Name),
                new { Type = item.RoleType?.ToString() }))
            .ToList();
        var batchRaw = await client.CreateEntitiesBatch("Role", batchItems);
        return CreateToolHelper.BuildBatchResponse(batchRaw, batchItems.Count);
    }
}

public sealed record RoleCreateRequest(
    [property: Description("One or more roles to create"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<RoleItem> Items);

public enum ScRoleType
{
    Archiver,
    AuxiliaryArchiver,
    AccessManager,
    LprManager,
    Federation,
    IntrusionDetection,
    AuthenticationService,
    BwcManager
}

public sealed record RoleItem(
    [property: Description("Role name"), Required, MinLength(1)] string Name,
    [Description("Role type")] ScRoleType? RoleType = null);
