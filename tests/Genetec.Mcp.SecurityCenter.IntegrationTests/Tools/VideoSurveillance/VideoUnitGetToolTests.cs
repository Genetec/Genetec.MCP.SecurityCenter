using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class VideoUnitGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_FirstVideoUnit_ReturnsRequestedGuidAndListedName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listResult = await VideoUnitListTool.Execute(sc.Manager, new VideoUnitListRequest(PageSize: 1, Page: 1));
        var unitId = ScIntegrationFixture.ExtractFirstGuid((string)listResult);
        Assert.NotNull(unitId);
        using var listDocument = IntegrationTestHelpers.ParseJson((string)listResult);
        var rows = IntegrationTestHelpers.GetResult(listDocument.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(listDocument.RootElement) : IntegrationTestHelpers.GetResult(listDocument.RootElement).GetProperty("Body");
        var firstUnit = rows[0];
        var expectedName = firstUnit.ValueKind == JsonValueKind.Array
            ? firstUnit[1].GetString() : firstUnit.GetProperty("Name").GetString();

        var result = await VideoUnitGetTool.Execute(sc.Manager, new VideoUnitGetTool.VideoUnitGetRequest(unitId!.Value));
        using var entityDocument = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(unitId.Value, IntegrationTestHelpers.GetResult(entityDocument.RootElement).GetProperty("Guid").GetGuid());
        Assert.Equal(expectedName, IntegrationTestHelpers.GetResult(entityDocument.RootElement).GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VideoUnitGetTool.Execute(sc.Manager,
            new VideoUnitGetTool.VideoUnitGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
