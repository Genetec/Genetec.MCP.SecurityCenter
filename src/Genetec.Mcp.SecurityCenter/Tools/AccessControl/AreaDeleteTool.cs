using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaDeleteTool
{
    [McpServerTool(
        Name = "area_delete",
        Title = "Delete Area",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Permanently delete one or more areas from Security Center. " +
        "Provide a single GUID or multiple GUIDs for batch deletion.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area delete request")] AreaDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "Area");
    }
}

public sealed record AreaDeleteRequest(
    [property: Description("One or more area GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
