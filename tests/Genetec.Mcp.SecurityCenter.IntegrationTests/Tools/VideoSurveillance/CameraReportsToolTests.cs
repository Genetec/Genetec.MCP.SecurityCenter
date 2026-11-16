using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraReportsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity()
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => CameraReportsTool.Execute(sc.Manager, new CameraReportsRequest(id, CameraReportAction.GetMetadataStreams)), "id", "UserGroup");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task EventReport_FirstCamera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var now = DateTime.UtcNow;
        var result = await CameraReportsTool.Execute(sc.Manager, new CameraReportsRequest(cameraId!.Value, CameraReportAction.EventReport, TimeStart: now.AddHours(-1).ToString("o"), TimeEnd: now.ToString("o")));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task VideoSequenceReport_FirstCamera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var result = await CameraReportsTool.Execute(sc.Manager, new CameraReportsRequest(cameraId!.Value, CameraReportAction.VideoSequenceReport));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetVideoFiles_FirstCamera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var result = await CameraReportsTool.Execute(sc.Manager, new CameraReportsRequest(cameraId!.Value, CameraReportAction.GetVideoFiles));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public async Task GetMetadataStreams_FirstCamera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var result = await CameraReportsTool.Execute(sc.Manager, new CameraReportsRequest(cameraId!.Value, CameraReportAction.GetMetadataStreams));
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}
