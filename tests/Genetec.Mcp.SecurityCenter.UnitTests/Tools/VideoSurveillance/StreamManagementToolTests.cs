using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using System.Text.Json;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class StreamManagementToolTests
{
    [Theory]
    [InlineData("UserGroup")]
    [InlineData("Camera")]
    [InlineData("")]
    public async Task Update_InvalidDevice_RejectsBeforeMutation(string entityType)
    {
        var streamId = Guid.NewGuid();
        var deviceId = entityType == "" ? Guid.Empty : Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(streamId, "Stream");
        if (!string.IsNullOrEmpty(entityType)) handler.AddEntity(deviceId, entityType);
        var request = JsonSerializer.Deserialize<StreamManagementRequest>(
            $$$"""{"action":"update","items":[{"id":"{{{streamId}}}","body":{"device":"{{{deviceId}}}"}}]}""",
            NumericStringConverter.ToolOptions)!;

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamManagementTool.Execute(TestHelpers.FakeManager(handler), request));

        Assert.Equal("items.body.device", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_DeviceGuard_AllowsDeviceOrOmittedReference(bool includeDevice)
    {
        var streamId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(streamId, "Stream");
        handler.AddEntity(deviceId, "Device");

        await StreamManagementTool.Execute(TestHelpers.FakeManager(handler),
            new StreamManagementRequest(StreamManagementAction.Update, Items:
                [new StreamManagementItem(Id: streamId,
                    Body: new StreamUpdateProperties(Name: "Test", Device: includeDevice ? deviceId : null))]));

        Assert.Equal(includeDevice, handler.Requests.Any(request =>
            request.Method == HttpMethod.Get &&
            Uri.UnescapeDataString(request.Uri.Query).Contains($"entity={deviceId}", StringComparison.Ordinal)));
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("UserGroup")]
    [InlineData("Camera")]
    [InlineData("")]
    public async Task Update_BatchInvalidDevice_PreservesOtherItem(string entityType)
    {
        var rejectedStreamId = Guid.NewGuid();
        var validStreamId = Guid.NewGuid();
        var deviceId = entityType == "" ? Guid.Empty : Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(rejectedStreamId, "Stream");
        handler.AddEntity(validStreamId, "Stream");
        if (!string.IsNullOrEmpty(entityType)) handler.AddEntity(deviceId, entityType);

        var result = await StreamManagementTool.Execute(TestHelpers.FakeManager(handler),
            new StreamManagementRequest(StreamManagementAction.Update, Items:
            [
                new StreamManagementItem(Id: rejectedStreamId, Body: new StreamUpdateProperties(Device: deviceId)),
                new StreamManagementItem(Id: validStreamId, Body: new StreamUpdateProperties(Name: "Test"))
            ]));

        using var document = JsonDocument.Parse(Assert.IsType<string>(result));
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("failure_count").GetInt32());
        var results = document.RootElement.GetProperty("results");
        Assert.False(results[0].GetProperty("success").GetBoolean());
        Assert.Equal(rejectedStreamId, results[0].GetProperty("id").GetGuid());
        Assert.Contains("items.body.device", results[0].GetProperty("error").GetString());
        Assert.True(results[1].GetProperty("success").GetBoolean());
        Assert.Equal(validStreamId, results[1].GetProperty("id").GetGuid());
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains($"entity={validStreamId}", Uri.UnescapeDataString(mutation.Uri.Query));
    }

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
