using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraUpdateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_FirstCamera_ReturnsOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, cameraId.Value, "Camera");
        var newName = $"{originalName}_mcp_test";
        try
        {
            var result = await CameraUpdateTool.Execute(sc.Manager,
                new CameraUpdateRequest([new CameraUpdateItem(cameraId.Value, Name: newName)]));
            Assert.IsType<string>(result);
            Assert.Equal(newName, await IntegrationTestHelpers.GetEntityNameAsync(sc, cameraId.Value, "Camera"));
        }
        finally
        {
            await CameraUpdateTool.Execute(sc.Manager,
                new CameraUpdateRequest([new CameraUpdateItem(cameraId.Value, Name: originalName)]));
        }
        Assert.Equal(originalName, await IntegrationTestHelpers.GetEntityNameAsync(sc, cameraId.Value, "Camera"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_WrongEntityType_RejectsSingleAndBatchAndPreservesEntity(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new CameraUpdateRequest(Enumerable.Range(0, count)
            .Select(_ => new CameraUpdateItem(id, Name: "Must not update")).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraUpdateTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await CameraUpdateTool.Execute(sc.Manager, request)));
            var result = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(0, result.GetProperty("success_count").GetInt32());
            Assert.Equal(count, result.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
