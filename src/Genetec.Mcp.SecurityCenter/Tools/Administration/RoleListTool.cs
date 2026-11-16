using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Administration;

[McpServerToolType]
public class RoleListTool
{
    [McpServerTool(
        Name = "role_list",
        Title = "List Roles",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "List a page of roles registered in Security Center. " +
        "Returns role names, GUIDs, and entity types. " +
        "Use small page_size values and page through results only as needed. Do not fetch all entities automatically; ask the user for permission before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Role list request")] RoleListRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.ListEntities("Role", request.PageSize, request.Page, ["Name"]);
    }
}

public sealed record RoleListRequest(
    [property: Description("Max results per page (1–100, default 50). Use small pages; ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number (1-based)"), Range(1, int.MaxValue)] int Page = 1);
