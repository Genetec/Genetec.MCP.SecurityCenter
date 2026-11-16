using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraPtzToolTests
{
    [Theory]
    [InlineData(PtzCommand.PanTilt)]
    [InlineData(PtzCommand.Zoom)]
    [InlineData(PtzCommand.Focus)]
    [InlineData(PtzCommand.Iris)]
    [InlineData(PtzCommand.GoToPreset)]
    [InlineData(PtzCommand.SavePreset)]
    [InlineData(PtzCommand.RunPattern)]
    [InlineData(PtzCommand.StartPattern)]
    [InlineData(PtzCommand.StopPattern)]
    [InlineData(PtzCommand.StartTour)]
    [InlineData(PtzCommand.StopTour)]
    [InlineData(PtzCommand.Lock)]
    [InlineData(PtzCommand.Unlock)]
    [InlineData(PtzCommand.MenuOn)]
    [InlineData(PtzCommand.MenuOff)]
    [InlineData(PtzCommand.MenuEnter)]
    [InlineData(PtzCommand.MenuLeft)]
    [InlineData(PtzCommand.MenuRight)]
    [InlineData(PtzCommand.MenuUp)]
    [InlineData(PtzCommand.MenuDown)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(PtzCommand action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => CameraPtzTool.Execute(manager, new CameraPtzRequest(id, action, PanSpeed: 0, TiltSpeed: 0, Speed: 0, PresetNumber: 1, PatternId: 1, TourId: 1, ZoomOperation: PtzZoomOperation.ZoomIn, FocusOperation: PtzFocusOperation.Near, IrisOperation: PtzIrisOperation.Open)), "id", "UserGroup");
    }

    private static readonly Guid CameraId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEnumerable<object[]> CommandMappings()
    {
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Zoom, Speed: 25, ZoomOperation: PtzZoomOperation.ZoomIn), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartZoom,0,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Zoom, Speed: 25, ZoomOperation: PtzZoomOperation.ZoomOut), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartZoom,1,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Focus, Speed: 25, FocusOperation: PtzFocusOperation.Near), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartFocus,0,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Focus, Speed: 25, FocusOperation: PtzFocusOperation.Far), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartFocus,1,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Iris, Speed: 25, IrisOperation: PtzIrisOperation.Open), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartIris,0,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.Iris, Speed: 25, IrisOperation: PtzIrisOperation.Close), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartIris,1,25)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.StartPattern, PatternId: 7), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StartPattern,7,0)"];
        yield return [new CameraPtzRequest(CameraId, PtzCommand.StopPattern, PatternId: 7), "SendPtzCommand(00000000-0000-0000-0000-000000000001,StopPattern,7,0)"];
    }

    [Theory]
    [MemberData(nameof(CommandMappings))]
    public void CommandMapping_UsesWebSdkParameterOrder(CameraPtzRequest request, string expected)
    {
        Assert.Equal(expected, CameraPtzTool.BuildActionQuery(request));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task PanTilt_MissingPanSpeed_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.PanTilt, TiltSpeed: 50)));
        Assert.Contains("pan_speed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PanTilt_MissingTiltSpeed_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.PanTilt, PanSpeed: 50)));
        Assert.Contains("tilt_speed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Focus_MissingSpeed_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.Focus)));
        Assert.Contains("speed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Zoom_MissingOperation_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.Zoom, Speed: 10)));
        Assert.Contains("zoom_operation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Focus_MissingOperation_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.Focus, Speed: 10)));
        Assert.Contains("focus_operation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Iris_MissingOperation_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.Iris, Speed: 10)));
        Assert.Contains("iris_operation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PtzCommand.GoToPreset)]
    [InlineData(PtzCommand.SavePreset)]
    public async Task PresetCommand_MissingPresetNumber_ThrowsArgumentException(PtzCommand command)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), command)));

        Assert.Contains("preset_number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunPattern_MissingPatternId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.RunPattern)));
        Assert.Contains("pattern_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartPattern_MissingPatternId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.StartPattern)));
        Assert.Contains("pattern_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StopPattern_MissingPatternId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.StopPattern)));
        Assert.Contains("pattern_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartTour_MissingTourId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraPtzTool.Execute(TestHelpers.FakeManager(),
                new CameraPtzRequest(Guid.NewGuid(), PtzCommand.StartTour)));
        Assert.Contains("tour_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
