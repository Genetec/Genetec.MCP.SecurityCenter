using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraThumbnailsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 320)]
    [InlineData(11, 1920)]
    [InlineData(50, 1281)]
    public async Task GetThumbnails_FirstCamera_ReturnsResult(int count, int width)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        using var camera = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient()
            .GetEntityFields(cameraId.Value.ToString(), "VideoEncoderDevice"));
        var encoderId = IntegrationTestHelpers.GetResult(camera.RootElement)
            .GetProperty("VideoEncoderDevice").GetGuid();
        var now = DateTime.UtcNow;
        var result = await CameraThumbnailsTool.Execute(sc.Manager, new CameraThumbnailsRequest(CameraIds: [cameraId!.Value], TimeStart: now.AddMinutes(-5).ToString("o"), TimeEnd: now.ToString("o"), Width: width, Count: count));
        using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        var rows = IntegrationTestHelpers.GetRows(document.RootElement);
        Assert.InRange(rows.GetArrayLength(), 1, count);
        foreach (var row in rows.EnumerateArray())
        {
            // SDK thumbnail routes return either the camera or its encoder;
            // both must identify the requested video source.
            Assert.Contains(IntegrationTestHelpers.GetReportField(document.RootElement, row, "Camera").GetGuid(),
                new[] { cameraId.Value, encoderId });
            Assert.NotEmpty(Convert.FromBase64String(
                IntegrationTestHelpers.GetReportField(document.RootElement, row, "Thumbnail").GetString()!));
        }
    }

    [Theory]
    [InlineData(6, 10, 640, false)]
    [InlineData(11, 1, 640, false)]
    [InlineData(1, 51, 640, false)]
    [InlineData(10, int.MaxValue, 640, false)]
    [InlineData(1, 0, 640, false)]
    [InlineData(1, 1, 0, false)]
    [InlineData(1, 1, 640, true)]
    public async Task InvalidWork_RejectsBeforeQuery(int cameras, int count, int width, bool reversed)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var request = new CameraThumbnailsRequest(Enumerable.Range(0, cameras).Select(_ => Guid.NewGuid()).ToList(),
            reversed ? "2026-10-23T00:02:00Z" : "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z", width, count);
        await Assert.ThrowsAsync<ArgumentException>(() => CameraThumbnailsTool.Execute(sc.Manager, request));
    }

    [Fact]
    public async Task CancelledCall_StopsBeforeSdk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CameraThumbnailsTool.Execute(sc.Manager,
            new CameraThumbnailsRequest([Guid.NewGuid()], "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z"), new CancellationToken(true)));
    }
}
