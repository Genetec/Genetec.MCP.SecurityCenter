using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class ParkingZoneConfigurationToolTests
{
    private static readonly Guid ZoneId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void SupportedActions_ContainOnlyDocumentedWebSdkActions()
    {
        Assert.Equal(
            [
                ParkingZoneConfigurationAction.GetCameras,
                ParkingZoneConfigurationAction.AddLprCamera,
                ParkingZoneConfigurationAction.RemoveLprCamera,
                ParkingZoneConfigurationAction.AddRule,
                ParkingZoneConfigurationAction.RemoveRule,
                ParkingZoneConfigurationAction.GetCapacity
            ],
            Enum.GetValues<ParkingZoneConfigurationAction>());
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.GetCameras)]
    [InlineData(ParkingZoneConfigurationAction.AddLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.RemoveLprCamera)]
    public async Task CameraActions_MissingCameraId_ThrowsArgumentException(
        ParkingZoneConfigurationAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingZoneConfigurationTool.Execute(
                TestHelpers.FakeManager(),
                new ParkingZoneConfigurationRequest(ZoneId, action, CameraId: null)));

        Assert.Contains("camera_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.AddRule)]
    [InlineData(ParkingZoneConfigurationAction.RemoveRule)]
    public async Task RuleActions_MissingRuleId_ThrowsArgumentException(
        ParkingZoneConfigurationAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingZoneConfigurationTool.Execute(
                TestHelpers.FakeManager(),
                new ParkingZoneConfigurationRequest(ZoneId, action, RuleId: null)));

        Assert.Contains("rule_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("position")]
    [InlineData("relative_motion")]
    public async Task AddLprCamera_MissingConditionalParameter_ThrowsArgumentException(
        string missingParameter)
    {
        var request = new ParkingZoneConfigurationRequest(
            ZoneId,
            ParkingZoneConfigurationAction.AddLprCamera,
            CameraId: DeviceId,
            Position: missingParameter == "position" ? null : ParkingZoneLprCameraPosition.Entrance,
            RelativeMotion: missingParameter == "relative_motion" ? null : ParkingZoneLprRelativeMotion.Ignored);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingZoneConfigurationTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains(missingParameter, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ParkingZoneConfigurationTool.Execute(
                TestHelpers.FakeManager(),
                new ParkingZoneConfigurationRequest(ZoneId, (ParkingZoneConfigurationAction)999)));

        Assert.Contains("Unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.GetCameras)]
    [InlineData(ParkingZoneConfigurationAction.AddLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.RemoveLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.AddRule)]
    [InlineData(ParkingZoneConfigurationAction.RemoveRule)]
    [InlineData(ParkingZoneConfigurationAction.GetCapacity)]
    public async Task TargetActions_WrongEntityType_RejectBeforeOperation(ParkingZoneConfigurationAction action)
    {
        var id = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        handler.AddEntity(linkedId, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ParkingZoneConfigurationRequest(id, action, CameraId: linkedId, RuleId: linkedId,
                Position: ParkingZoneLprCameraPosition.Entrance, RelativeMotion: ParkingZoneLprRelativeMotion.Ignored)));
        Assert.Equal("zone_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(ParkingZoneConfigurationAction.GetCameras)]
    [InlineData(ParkingZoneConfigurationAction.AddLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.RemoveLprCamera)]
    [InlineData(ParkingZoneConfigurationAction.AddRule)]
    [InlineData(ParkingZoneConfigurationAction.RemoveRule)]
    public async Task LinkedActions_WrongEntityType_RejectBeforeMutation(ParkingZoneConfigurationAction action)
    {
        var id = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "ParkingZone");
        handler.AddEntity(linkedId, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneConfigurationTool.Execute(TestHelpers.FakeManager(handler),
            new ParkingZoneConfigurationRequest(id, action, CameraId: linkedId, RuleId: linkedId,
                Position: ParkingZoneLprCameraPosition.Entrance, RelativeMotion: ParkingZoneLprRelativeMotion.Ignored)));
        Assert.Equal(action is ParkingZoneConfigurationAction.AddRule or ParkingZoneConfigurationAction.RemoveRule ? "rule_id" : "camera_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
