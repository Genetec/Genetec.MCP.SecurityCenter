using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraPtzToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
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
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(PtzCommand action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => CameraPtzTool.Execute(sc.Manager, new CameraPtzRequest(id, action, PanSpeed: 0, TiltSpeed: 0, Speed: 0, PresetNumber: 1, PatternId: 1, TourId: 1, ZoomOperation: PtzZoomOperation.ZoomIn, FocusOperation: PtzFocusOperation.Near, IrisOperation: PtzIrisOperation.Open)), "id", "UserGroup");
    }

        public static IEnumerable<object[]> PtzScenarios()
        {
                var id = Guid.Empty;
                yield return [new CameraPtzRequest(id, PtzCommand.PanTilt, 0, 0)];
                yield return [new CameraPtzRequest(id, PtzCommand.Zoom, Speed: 10, ZoomOperation: PtzZoomOperation.ZoomIn)];
                yield return [new CameraPtzRequest(id, PtzCommand.Zoom, Speed: 10, ZoomOperation: PtzZoomOperation.ZoomOut)];
                yield return [new CameraPtzRequest(id, PtzCommand.Focus, Speed: 10, FocusOperation: PtzFocusOperation.Near)];
                yield return [new CameraPtzRequest(id, PtzCommand.Focus, Speed: 10, FocusOperation: PtzFocusOperation.Far)];
                yield return [new CameraPtzRequest(id, PtzCommand.Iris, Speed: 10, IrisOperation: PtzIrisOperation.Open)];
                yield return [new CameraPtzRequest(id, PtzCommand.Iris, Speed: 10, IrisOperation: PtzIrisOperation.Close)];
                yield return [new CameraPtzRequest(id, PtzCommand.GoToPreset, PresetNumber: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.SavePreset, PresetNumber: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.RunPattern, PatternId: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.StartPattern, PatternId: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.StopPattern, PatternId: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.StartTour, TourId: 1)];
                yield return [new CameraPtzRequest(id, PtzCommand.StopTour)];
                yield return [new CameraPtzRequest(id, PtzCommand.Lock)];
                yield return [new CameraPtzRequest(id, PtzCommand.Unlock)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuOn)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuOff)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuEnter)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuLeft)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuRight)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuUp)];
                yield return [new CameraPtzRequest(id, PtzCommand.MenuDown)];
        }

        [Theory]
        [MemberData(nameof(PtzScenarios))]
        public async Task PtzCommand_OnFirstCamera_UsesSdkSignature(CameraPtzRequest scenario)
        {
                Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
                Assert.NotNull(cameraId);

                var request = scenario with { Id = cameraId.Value };
                var result = Assert.IsType<string>(await CameraPtzTool.Execute(sc.Manager, request));
                Assert.DoesNotContain("\"error\"", result, StringComparison.OrdinalIgnoreCase);
        }
}
