using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AreaListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task List_ExcludesHiddenAndExternalSystemRoots_PreservesVisibleAreasAndPaging()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var defaults = JsonSerializer.Deserialize<AreaListRequest>("{}", NumericStringConverter.ToolOptions)!;
        Assert.False(defaults.ShowHiddenAreas);
        Assert.False(defaults.ShowGroupRoots);
        var client = sc.Manager.GetClient();
        var visibleName = $"MCP_VisibleArea_{Guid.NewGuid():N}";
        var visible = Assert.IsType<EntityCreateResult>(await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem(visibleName)])));
        sc.TrackForCleanup(visible.Guid);
        var hidden = Assert.IsType<EntityCreateResult>(await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_HiddenArea_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(hidden.Guid);
        await client.UpdateEntity(hidden.Guid.ToString(), new { HiddenFromUI = true });

        var hiddenEntity = Assert.IsType<AreaVisibilityRead>(JsonSerializer.Deserialize<AreaVisibilityRead>(
            await client.GetEntityFields(hidden.Guid.ToString(), "Guid", "HiddenFromUI", "IsExternalSystemRootArea")));
        Assert.Equal(hidden.Guid, hiddenEntity.Guid);
        Assert.True(hiddenEntity.HiddenFromUI);
        Assert.False(hiddenEntity.IsExternalSystemRootArea);

        async Task<AreaListPage<AreaListEntry>> ReadPage(int size, int page)
        {
            var json = await client.ListEntities("Area", size, page,
                ["Name", "HiddenFromUI", "IsExternalSystemRootArea"], includeEnvelope: true);
            return Assert.IsType<AreaListEnvelope<AreaListEntry>>(
                JsonSerializer.Deserialize<AreaListEnvelope<AreaListEntry>>(json, NumericStringConverter.ToolOptions)).Rsp;
        }

        static List<AreaListItem> AssertListedAreas(object result, string status)
        {
            if (status == "Ok") return Assert.IsType<List<AreaListItem>>(result);
            var envelope = Assert.IsType<AreaListEnvelope<AreaListItem>>(result);
            Assert.Equal(status, envelope.Rsp.Status);
            return envelope.Rsp.Result;
        }

        var rawPage = await ReadPage(100, 1);
        Assert.Equal("Ok", rawPage.Status);
        Assert.Contains(rawPage.Result, area => area.Guid == hidden.Guid);
        Assert.Contains(rawPage.Result, area => area.IsExternalSystemRootArea == "True");
        var listedAreas = AssertListedAreas(
            await AreaListTool.Execute(sc.Manager, new AreaListRequest(PageSize: 100)), rawPage.Status);
        Assert.Contains(listedAreas, area => area.Guid == visible.Guid && area.Name == visibleName);
        Assert.DoesNotContain(listedAreas, area => area.Guid == hidden.Guid);

        // Cover each visibility combination and the existing pagination cases.
        foreach (var (showHidden, showRoots) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            foreach (var size in new[] { 1, 2, 100 })
            {
                var pageCount = Math.Max(2, (rawPage.Result.Count + size - 1) / size);
                for (var page = 1; page <= pageCount; page++)
                {
                    var raw = await ReadPage(size, page);
                    var json = JsonSerializer.Serialize(new AreaListRequest(PageSize: size, Page: page,
                        ShowHiddenAreas: showHidden, ShowGroupRoots: showRoots), NumericStringConverter.ToolOptions);
                    var request = JsonSerializer.Deserialize<AreaListRequest>(json, NumericStringConverter.ToolOptions)!;
                    Assert.Equal(showHidden, request.ShowHiddenAreas);
                    Assert.Equal(showRoots, request.ShowGroupRoots);
                    var actual = AssertListedAreas(await AreaListTool.Execute(sc.Manager, request), raw.Status);
                    var expected = raw.Result.Where(area => (showHidden || !area.IsHidden) &&
                            (showRoots || !area.IsGroupRoot))
                        .Select(area => new AreaListItem(area.Guid, area.Name)).ToList();
                    Assert.Equal(expected, actual);
                    Assert.InRange(actual.Count, 0, size);
                    if (size == 100 && page == 1)
                    {
                        Assert.Contains(actual, area => area.Guid == visible.Guid && area.Name == visibleName);
                        Assert.Equal(showHidden, actual.Any(area => area.Guid == hidden.Guid));
                        foreach (var root in raw.Result.Where(area => area.IsGroupRoot))
                            Assert.Equal(showRoots && (showHidden || !root.IsHidden),
                                actual.Any(area => area.Guid == root.Guid));
                    }
                }
            }
        }

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            AreaListTool.Execute(sc.Manager, new AreaListRequest(PageSize: 101)));
        Assert.Equal("page_size", error.ParamName);
    }

    private sealed record AreaVisibilityRead(Guid Guid, bool HiddenFromUI, bool IsExternalSystemRootArea);

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAreaObjects()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await AreaListTool.Execute(sc.Manager, new AreaListRequest());

        Assert.True(result is List<AreaListItem> or AreaListEnvelope<AreaListItem>);
    }

    [Fact]
    public async Task List_WithPagination_ReturnsAreaObjects()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await AreaListTool.Execute(sc.Manager, new AreaListRequest(PageSize: 10, Page: 1));

        Assert.True(result is List<AreaListItem> or AreaListEnvelope<AreaListItem>);
    }
}
