using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraRecordingToolTests
{
    [Theory]
    [InlineData(CameraRecordingAction.StartRecording)]
    [InlineData(CameraRecordingAction.StopRecording)]
    [InlineData(CameraRecordingAction.GetStreams)]
    [InlineData(CameraRecordingAction.EnableMaintenance)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(CameraRecordingAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => CameraRecordingTool.Execute(manager, new CameraRecordingRequest(id, action, RecordingLength: 10, Locked: false, EndTime: DateTime.UtcNow.AddMinutes(1))), "id", "UserGroup");
    }

    // ── Surface ─────────────────────────────────────────────────────

    [Fact]
    public void SupportedActions_ContainAllRecordingOperations()
    {
        Assert.Equal(
            [
                CameraRecordingAction.StartRecording,
                CameraRecordingAction.StopRecording,
                CameraRecordingAction.GetStreams,
                CameraRecordingAction.EnableMaintenance
            ],
            Enum.GetValues<CameraRecordingAction>());
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(10, false, "00:00:10,false")]
    [InlineData(65, true, "00:01:05,true")]
    [InlineData(90061, false, "1.01:01:01,false")]
    public async Task StartThenStopRecording_UsesDurationAndSameCamera(
        int seconds, bool locked, string expectedArguments)
    {
        var handler = new ToolTestHandler();
        var metadata = new EntityMetadataTestHandler(handler);
        var manager = TestHelpers.FakeManager(metadata);
        var cameraId = Guid.NewGuid();
        metadata.AddEntity(cameraId, "Camera");
        var requestJson = $$"""{"id":"{{cameraId}}","action":"start_recording","recording_length":{{seconds}},"locked":{{locked.ToString().ToLowerInvariant()}}}""";
        var request = JsonSerializer.Deserialize<CameraRecordingRequest>(requestJson, NumericStringConverter.ToolOptions)!;

        await CameraRecordingTool.Execute(manager, request);
        await CameraRecordingTool.Execute(manager,
            new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording));

        Assert.Collection(handler.Requests,
            uri => Assert.Equal($"?q=entity={cameraId},StartRecording({expectedArguments})",
                Uri.UnescapeDataString(uri.Query)),
            uri => Assert.Equal($"?q=entity={cameraId},StopRecording()",
                Uri.UnescapeDataString(uri.Query)));
    }

    [Theory]
    [InlineData("recording_length")]
    [InlineData("locked")]
    public async Task StartRecording_MissingConditionalParameter_ThrowsArgumentException(string missingParameter)
    {
        var request = new CameraRecordingRequest(
            Guid.NewGuid(),
            CameraRecordingAction.StartRecording,
            RecordingLength: missingParameter == "recording_length" ? null : 1,
            Locked: missingParameter == "locked" ? null : false);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraRecordingTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains(missingParameter, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartRecording_RecordingLengthBelowMinimum_FailsValidation()
    {
        var request = new CameraRecordingRequest(
            Guid.NewGuid(),
            CameraRecordingAction.StartRecording,
            RecordingLength: 0,
            Locked: false);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CameraRecordingRequest.RecordingLength)));
    }

    [Fact]
    public async Task EnableMaintenance_MissingEndTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraRecordingTool.Execute(TestHelpers.FakeManager(),
                new CameraRecordingRequest(Guid.NewGuid(), CameraRecordingAction.EnableMaintenance)));
        Assert.Contains("end_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnableMaintenance_UsesTypedEndTimeAndEscapesReason()
    {
        var handler = new ToolTestHandler();
        var endTime = new DateTime(2026, 8, 27, 18, 5, 42, DateTimeKind.Utc);
        var request = new CameraRecordingRequest(
            Guid.NewGuid(),
            CameraRecordingAction.EnableMaintenance,
            EndTime: endTime,
            Reason: "maintenance, verification");

        var metadata = new EntityMetadataTestHandler(handler);
        metadata.AddEntity(request.Id, "Camera");
        await CameraRecordingTool.Execute(TestHelpers.FakeManager(metadata), request);

        var uri = Uri.UnescapeDataString(Assert.Single(handler.Requests).ToString());
        var expectedEndTime = endTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        Assert.Contains(
            $"EnableMaintenance({expectedEndTime},maintenance\\, verification)",
            uri,
            StringComparison.Ordinal);
    }
}
