using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class StreamManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData("wrong_type")]
    [InlineData("empty")]
    public async Task Update_InvalidDevice_RejectsAndPreservesStream(string scenario)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var streamId = await GetFirstStreamIdAsync();
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, streamId, "Stream");
        var deviceId = scenario switch
        {
            "wrong_type" => wrongId,
            "empty" => Guid.Empty,
            _ => throw new ArgumentException("Unknown test scenario.", nameof(scenario))
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => StreamManagementTool.Execute(sc.Manager,
            new StreamManagementRequest(StreamManagementAction.Update, Items:
                [new StreamManagementItem(Id: streamId,
                    Body: new StreamUpdateProperties(Name: "Must not update", Device: deviceId))])));

        Assert.Equal("items.body.device", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, streamId, "Stream", originalName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Theory]
    [InlineData("wrong_type")]
    [InlineData("empty")]
    public async Task Update_BatchInvalidDevice_PreservesSuccessfulUpdate(string scenario)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var streamId = await GetFirstStreamIdAsync();
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, streamId, "Stream");
        var updatedName = $"MCP_Stream_{Guid.NewGuid():N}";
        var deviceId = scenario switch
        {
            "wrong_type" => wrongId,
            "empty" => Guid.Empty,
            _ => throw new ArgumentException("Unknown test scenario.", nameof(scenario))
        };
        try
        {
            var result = await StreamManagementTool.Execute(sc.Manager,
                new StreamManagementRequest(StreamManagementAction.Update, Items:
                [
                    new StreamManagementItem(Id: streamId, Body: new StreamUpdateProperties(Name: "Must not update", Device: deviceId)),
                    new StreamManagementItem(Id: streamId, Body: new StreamUpdateProperties(Name: updatedName))
                ]));

            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
            var batch = document.RootElement;
            Assert.Equal(1, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(1, batch.GetProperty("failure_count").GetInt32());
            var results = batch.GetProperty("results");
            Assert.False(results[0].GetProperty("success").GetBoolean());
            Assert.Equal(streamId, results[0].GetProperty("id").GetGuid());
            Assert.Contains("items.body.device", results[0].GetProperty("error").GetString());
            Assert.True(results[1].GetProperty("success").GetBoolean());
            Assert.Equal(streamId, results[1].GetProperty("id").GetGuid());
            await IntegrationTestHelpers.AssertEntityAsync(sc, streamId, "Stream", updatedName);
            await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
        }
        finally
        {
            await StreamManagementTool.Execute(sc.Manager,
                new StreamManagementRequest(StreamManagementAction.Update, Items:
                    [new StreamManagementItem(Id: streamId, Body: new StreamUpdateProperties(Name: originalName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, streamId, "Stream", originalName);
        }
    }

    private async Task<Guid> GetFirstStreamIdAsync()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        using var document = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().ListEntities("Stream", 50, 1));
        var rows = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, rows.ValueKind);
        Assert.True(rows.GetArrayLength() > 0, "This test requires a configured Stream.");
        return rows[0].GetProperty("Guid").GetGuid();
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task ListByCamera_FirstCamera_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var result = await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.ListByCamera, CameraId: cameraId));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_FirstStream_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        Guid? cameraId = await IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);
        Assert.NotNull(cameraId);
        var streamsResult = (string)await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.ListByCamera, CameraId: cameraId));
        using var streams = System.Text.Json.JsonDocument.Parse(streamsResult);
        var streamIds = IntegrationTestHelpers.ExtractGuids(streams.RootElement.GetProperty("Streams").GetRawText());
        Assert.NotEmpty(streamIds);
        var streamId = streamIds[0];
        var result = await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.Get, Id: streamId));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUpdate_ExistingTarget_ReadsBackAndRestoresName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var targets = await sc.Manager.GetClient().ListEntities("Stream", 50, 1);
        using var document = IntegrationTestHelpers.ParseJson(targets);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, rows.ValueKind);
        Assert.True(rows.GetArrayLength() > 0, "This test requires a configured Stream.");
        var id = rows[0].GetProperty("Guid").GetGuid();
        var originalName = await IntegrationTestHelpers.GetEntityNameAsync(sc, id, "Stream");
        var result = await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.Get, Id: id));
        using var read = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(id, IntegrationTestHelpers.GetResult(read.RootElement).GetProperty("Guid").GetGuid());
        var updatedName = $"MCP_Stream_{Guid.NewGuid():N}";
        try
        {
            await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.Update, Items: [new StreamManagementItem(Id: id, Body: new StreamUpdateProperties(Name: updatedName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Stream", updatedName);
        }
        finally
        {
            await StreamManagementTool.Execute(sc.Manager, new StreamManagementRequest(StreamManagementAction.Update, Items: [new StreamManagementItem(Id: id, Body: new StreamUpdateProperties(Name: originalName))]));
            await IntegrationTestHelpers.AssertEntityAsync(sc, id, "Stream", originalName);
        }
    }


    [Theory]
    [InlineData(StreamManagementAction.Get)]
    [InlineData(StreamManagementAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(StreamManagementAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action is StreamManagementAction.Update ? new StreamManagementRequest(action, Items: [new StreamManagementItem(Id: id, Body: action == StreamManagementAction.Update ? new StreamUpdateProperties(Name: "Must not update") : null)]) : new StreamManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StreamManagementTool.Execute(sc.Manager, request));
        Assert.Equal(action is StreamManagementAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Fact]
    public async Task ListByCamera_WrongEntityType_RejectsCameraTarget()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StreamManagementTool.Execute(sc.Manager,
            new StreamManagementRequest(StreamManagementAction.ListByCamera, CameraId: id)));
        Assert.Equal("camera_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
