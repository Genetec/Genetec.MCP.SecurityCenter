using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class VisitorListTool
{
    [McpServerTool(
        Name = "visitor_list",
        Title = "List Visitors",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "List a page of visitors registered in Security Center. " +
        "Returns visitor names, GUIDs, and entity types. " +
        "Use small page_size values and page through results only as needed. Do not fetch all entities automatically; ask the user for permission before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Visitor list request")] VisitorListRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.ListEntities("Visitor", request.PageSize, request.Page, ["Name"]);
    }
}

public sealed record VisitorListRequest(
    [property: Description("Max results per page (1–100, default 50). Use small pages; ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number (1-based)"), Range(1, int.MaxValue)] int Page = 1);
