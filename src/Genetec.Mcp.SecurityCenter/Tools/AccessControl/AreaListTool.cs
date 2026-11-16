using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.AccessControl;

[McpServerToolType]
public class AreaListTool
{
    [McpServerTool(
        Name = "area_list",
        Title = "List Areas",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "List a page of areas in Security Center. " +
        "Returns area names, GUIDs, and entity types. " +
        "Hidden areas and external-system grouping roots are excluded by default. " +
        "Use show_hidden_areas and show_group_roots to include them; an area that is both requires both options. " +
        "Use small page_size values and page through results only as needed. Do not fetch all entities automatically; ask the user for permission before running large queries.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Area list request")] AreaListRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        var response = await client.ListEntities("Area", request.PageSize, request.Page,
            ["Name", "HiddenFromUI", "IsExternalSystemRootArea"], includeEnvelope: true);
        var envelope = JsonSerializer.Deserialize<AreaListEnvelope<AreaListEntry>>(response, NumericStringConverter.ToolOptions)
            ?? throw new InvalidOperationException("Security Center returned an empty area list response.");
        var page = envelope.Rsp;
        var areas = page.Result.Where(area => (request.ShowHiddenAreas || !area.IsHidden) &&
                (request.ShowGroupRoots || !area.IsGroupRoot))
            .Select(area => new AreaListItem(area.Guid, area.Name)).ToList();
        return page.Status == "Ok" ? areas
            : new AreaListEnvelope<AreaListItem>(new AreaListPage<AreaListItem>(page.Status, areas));
    }
}

public sealed record AreaListRequest(
    [property: Description("Max results per page (1–100, default 50). Use small pages; ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [Description("Include areas hidden from the UI (default false). Hidden grouping roots also require show_group_roots.")] bool ShowHiddenAreas = false,
    [Description("Include external-system grouping roots, such as federation roots (default false). Hidden roots also require show_hidden_areas.")] bool ShowGroupRoots = false);
