using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class VideoUnitUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_FirstVideoUnit_ReturnsOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var listResult = await VideoUnitListTool.Execute(sc.Manager, new VideoUnitListRequest(PageSize: 1, Page: 1));
        var unitId = IntegrationTestHelpers.ExtractFirstGuid(Assert.IsType<string>(listResult));
        Assert.NotNull(unitId);
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, unitId.Value, "VideoUnit");
        var newName = $"{originalName}_mcp_test";
        try
        {
            var result = await VideoUnitUpdateTool.Execute(sc.Manager,
                new VideoUnitUpdateRequest([new VideoUnitUpdateItem(unitId.Value, Name: newName)]));
            Assert.IsType<string>(result);
            Assert.Equal(newName, await IntegrationTestHelpers.GetEntityNameAsync(sc, unitId.Value, "VideoUnit"));
        }
        finally
        {
            await VideoUnitUpdateTool.Execute(sc.Manager,
                new VideoUnitUpdateRequest([new VideoUnitUpdateItem(unitId.Value, Name: originalName)]));
        }
        Assert.Equal(originalName, await IntegrationTestHelpers.GetEntityNameAsync(sc, unitId.Value, "VideoUnit"));
    }

    [Fact]
    public async Task Update_UserGroupGuidAndMixedBatch_RejectWrongTypeAndRestoreVideoUnit()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var singleError = await Assert.ThrowsAsync<ArgumentException>(() => VideoUnitUpdateTool.Execute(sc.Manager,
            new VideoUnitUpdateRequest([new VideoUnitUpdateItem(wrongTarget.Id, Name: "Must not update")] )));
        Assert.Equal("items", singleError.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var listResult = await VideoUnitListTool.Execute(sc.Manager, new VideoUnitListRequest(PageSize: 1, Page: 1));
        var unitId = IntegrationTestHelpers.ExtractFirstGuid(Assert.IsType<string>(listResult));
        Assert.NotNull(unitId);
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, unitId.Value, "VideoUnit");
        var updatedName = $"{originalName}_type_guard_test";

        try
        {
            var batch = await VideoUnitUpdateTool.Execute(sc.Manager, new VideoUnitUpdateRequest([
                new VideoUnitUpdateItem(unitId.Value, Name: updatedName),
                new VideoUnitUpdateItem(wrongTarget.Id, Name: "Must not update")
            ]));
            IntegrationTestHelpers.AssertPartialBatchResult(batch, unitId.Value, wrongTarget.Id);
            Assert.Equal(updatedName, await IntegrationTestHelpers.GetEntityNameAsync(sc, unitId.Value, "VideoUnit"));
            await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
        }
        finally
        {
            await VideoUnitUpdateTool.Execute(sc.Manager,
                new VideoUnitUpdateRequest([new VideoUnitUpdateItem(unitId.Value, Name: originalName)]));
        }
    }
}
