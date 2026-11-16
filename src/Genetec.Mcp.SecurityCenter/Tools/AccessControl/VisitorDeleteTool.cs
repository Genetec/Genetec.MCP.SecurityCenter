using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorDeleteTool
{
    [McpServerTool(
        Name = "visitor_delete",
        Title = "Delete Visitor",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description(
        "Permanently delete one or more visitors from Security Center. " +
        "Provide a single GUID or multiple GUIDs for batch deletion.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor delete request")] VisitorDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await DeleteToolHelper.Execute(client, request.Items, "Visitor");
    }
}

public sealed record VisitorDeleteRequest(
    [property: Description("One or more visitor GUIDs to delete"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<Guid> Items);
