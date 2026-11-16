using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class StreamManagementToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamManagementTool.Execute(TestHelpers.FakeManager(),
                new StreamManagementRequest(StreamManagementAction.Get, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListByCamera_NullCameraId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamManagementTool.Execute(TestHelpers.FakeManager(),
                new StreamManagementRequest(StreamManagementAction.ListByCamera, CameraId: null)));

        Assert.Contains("camera_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamManagementTool.Execute(TestHelpers.FakeManager(),
                new StreamManagementRequest(StreamManagementAction.Update, Items: [new StreamManagementItem(Id: null, Body: new StreamUpdateProperties(Name: "test"))])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullBody_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamManagementTool.Execute(TestHelpers.FakeManager(),
                new StreamManagementRequest(StreamManagementAction.Update, Items: [new StreamManagementItem(Id: Guid.NewGuid(), Body: null)])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData(StreamManagementAction.Get)]
    [InlineData(StreamManagementAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(StreamManagementAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = (action is StreamManagementAction.Update ? new StreamManagementRequest(action, Items: [new StreamManagementItem(Id: id, Body: action == StreamManagementAction.Update ? new StreamUpdateProperties(Name: "Must not update") : null)]) : new StreamManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StreamManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action is StreamManagementAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task ListByCamera_WrongEntityType_RejectsCameraTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StreamManagementTool.Execute(TestHelpers.FakeManager(handler),
            new StreamManagementRequest(StreamManagementAction.ListByCamera, CameraId: id)));
        Assert.Equal("camera_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
