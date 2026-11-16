using System.Globalization;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraRecordingToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CameraRecordingAction.StartRecording)]
    [InlineData(CameraRecordingAction.StopRecording)]
    [InlineData(CameraRecordingAction.GetStreams)]
    [InlineData(CameraRecordingAction.EnableMaintenance)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(CameraRecordingAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => CameraRecordingTool.Execute(sc.Manager, new CameraRecordingRequest(id, action, RecordingLength: 10, Locked: false, EndTime: DateTime.UtcNow.AddMinutes(1))), "id", "UserGroup");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task RecordTenSecondsThenGetThumbnails_ReturnsImageFromRecordedInterval()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var cameraId = await GetFirstCameraIdAsync();
        using var camera = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(cameraId.ToString()));
        Assert.Equal(cameraId, IntegrationTestHelpers.GetResult(camera.RootElement).GetProperty("Guid").GetGuid());
        var started = false;
        try
        {
            var start = DateTime.UtcNow;
            var startResult = Assert.IsType<string>(await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(cameraId, CameraRecordingAction.StartRecording,
                    RecordingLength: 10, Locked: false)));
            started = true;
            using var startResponse = JsonDocument.Parse(startResult);
            Assert.Equal("Ok", IntegrationTestHelpers.GetResult(startResponse.RootElement).GetProperty("status").GetString());
            string? recordingState = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                using var state = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityField(cameraId.ToString(), "RecordingState"));
                recordingState = IntegrationTestHelpers.GetResult(state.RootElement).GetProperty("RecordingState").GetString();
                if (recordingState is "On" or "OnLocked" or "OnAboutToStop") break;
                if (attempt < 4) await Task.Delay(TimeSpan.FromSeconds(1));
            }
            Assert.Contains(recordingState, new[] { "On", "OnLocked", "OnAboutToStop" });
            var remaining = start.AddSeconds(10) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
            var end = DateTime.UtcNow;

            var stopResult = Assert.IsType<string>(await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording)));
            using var stopResponse = JsonDocument.Parse(stopResult);
            Assert.Equal("Ok", IntegrationTestHelpers.GetResult(stopResponse.RootElement).GetProperty("status").GetString());
            started = false;

            var hasArchive = false;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                using var archive = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().RunReport("VideoSequence",
                    $"Cameras@{cameraId},TimeRange.SetTimeRange({start:O},{end:O}),MaximumResultCount=10"));
                var rows = IntegrationTestHelpers.GetRows(archive.RootElement);
                foreach (var row in rows.EnumerateArray())
                {
                    Assert.Equal(cameraId, IntegrationTestHelpers.GetReportField(archive.RootElement, row, "CameraGuid").GetGuid());
                    if (IntegrationTestHelpers.GetReportField(archive.RootElement, row, "StartTime").GetDateTime().ToUniversalTime() <= end &&
                        IntegrationTestHelpers.GetReportField(archive.RootElement, row, "EndTime").GetDateTime().ToUniversalTime() >= start)
                        hasArchive = true;
                }
                if (hasArchive) break;
                if (attempt < 9) await Task.Delay(TimeSpan.FromSeconds(3));
            }
            Assert.True(hasArchive, $"No archived video sequence overlaps the recording interval for camera {cameraId}.");

            // Allow the Archiver to make the just-recorded frames available.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var result = Assert.IsType<string>(await CameraThumbnailsTool.Execute(sc.Manager,
                    new CameraThumbnailsRequest([cameraId],
                        start.ToString("O", CultureInfo.InvariantCulture),
                        end.ToString("O", CultureInfo.InvariantCulture), Width: 320, Count: 3)));
                using var response = JsonDocument.Parse(result);
                Assert.Equal(JsonValueKind.Array, IntegrationTestHelpers.GetResult(response.RootElement).ValueKind);
                foreach (var thumbnail in IntegrationTestHelpers.GetResult(response.RootElement).EnumerateArray())
                {
                    var encodedImage = thumbnail.GetProperty("Thumbnail").GetString();
                    if (string.IsNullOrEmpty(encodedImage)) continue;

                    Assert.Equal(cameraId, thumbnail.GetProperty("Camera").GetGuid());
                    var timestamp = thumbnail.GetProperty("Timestamp").GetDateTime().ToUniversalTime();
                    Assert.InRange(timestamp, start, end);
                    var image = Convert.FromBase64String(encodedImage);
                    Assert.True(image.Length > 4, "Thumbnail must contain image bytes.");
                    Assert.True((image[0] == 0xff && image[1] == 0xd8) ||
                        (image[0] == 0x89 && image[1] == 0x50 && image[2] == 0x4e && image[3] == 0x47),
                        "Thumbnail must contain a JPEG or PNG image.");
                    return;
                }

                if (attempt < 9) await Task.Delay(TimeSpan.FromSeconds(3));
            }

            Assert.Fail("No image was returned for the ten-second recording after waiting for the Archiver.");
        }
        finally
        {
            if (started)
                await CameraRecordingTool.Execute(sc.Manager,
                    new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording));
        }
    }

    [Fact]
    public async Task StartRecording_ExistingCamera_ReturnsOkAndCanBeStopped()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();
        var started = false;

        try
        {
            var result = await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(
                    cameraId,
                    CameraRecordingAction.StartRecording,
                    RecordingLength: 5,
                    Locked: false));

            started = true;
            AssertOk(result);
            await AssertVideoFileReportAsync(cameraId);
        }
        finally
        {
            if (started)
            {
                AssertOk(await CameraRecordingTool.Execute(sc.Manager,
                    new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording)));
            }
        }
    }

    [Fact]
    public async Task StopRecording_ExistingCamera_ReturnsOkAndReportReadsBack()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();
        var started = false;

        try
        {
            var startResult = await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(
                    cameraId,
                    CameraRecordingAction.StartRecording,
                    RecordingLength: 5,
                    Locked: false));
            started = true;
            AssertOk(startResult);

            AssertOk(await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording)));
            started = false;
            await AssertVideoFileReportAsync(cameraId);
        }
        finally
        {
            if (started)
            {
                // StopRecording is idempotent for the cleanup path if the assertion above fails.
                await CameraRecordingTool.Execute(sc.Manager,
                    new CameraRecordingRequest(cameraId, CameraRecordingAction.StopRecording));
            }
        }
    }

    [Fact]
    public async Task GetStreams_ExistingCamera_ReturnsNonEmptyStreams()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();

        var result = Assert.IsType<string>(await CameraRecordingTool.Execute(sc.Manager,
            new CameraRecordingRequest(cameraId, CameraRecordingAction.GetStreams)));

        using var document = JsonDocument.Parse(result);
        var root = IntegrationTestHelpers.GetResult(document.RootElement);
        var streams = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("Streams", out var value) ? value : default;

        Assert.Equal(JsonValueKind.Array, streams.ValueKind);
        Assert.NotEmpty(streams.EnumerateArray());
    }

    [Fact]
    public async Task EnableMaintenance_ExistingCamera_PersistsStateAndCleanupRestoresIt()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();
        var reason = $"MCP_CameraRecording_{Guid.NewGuid():N}";
        var expectedEndTime = DateTime.UtcNow.AddMinutes(2);

        try
        {
            var result = await CameraRecordingTool.Execute(sc.Manager,
                new CameraRecordingRequest(
                    cameraId,
                    CameraRecordingAction.EnableMaintenance,
                    EndTime: expectedEndTime,
                    Reason: reason));

            AssertOk(result);

            var entityJson = await sc.Manager.GetClient().GetEntity(cameraId.ToString());
            using var entity = JsonDocument.Parse(entityJson);
            var root = IntegrationTestHelpers.GetResult(entity.RootElement);
            Assert.True(root.TryGetProperty("IsInMaintenance", out var isInMaintenance));
            Assert.True(isInMaintenance.GetBoolean());
            Assert.True(root.TryGetProperty("MaintenanceEndTime", out var maintenanceEndTime));
            Assert.True(DateTimeOffset.TryParse(maintenanceEndTime.GetString(), out var actualEndTime));
            Assert.InRange(actualEndTime,
                new DateTimeOffset(expectedEndTime).AddSeconds(-5),
                new DateTimeOffset(expectedEndTime).AddSeconds(5));
        }
        finally
        {
            await sc.Manager.GetClient().ExecuteEntityMethod(
                cameraId.ToString(), "DisableMaintenance");

            var entityJson = await sc.Manager.GetClient().GetEntity(cameraId.ToString());
            using var entity = JsonDocument.Parse(entityJson);
            var root = IntegrationTestHelpers.GetResult(entity.RootElement);
            Assert.True(root.TryGetProperty("IsInMaintenance", out var isInMaintenance));
            Assert.False(isInMaintenance.GetBoolean());
        }
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private Task<Guid> GetFirstCameraIdAsync()
        => IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);

    private async Task AssertVideoFileReportAsync(Guid cameraId)
    {
        var result = Assert.IsType<string>(await CameraReportsTool.Execute(sc.Manager,
            new CameraReportsRequest(cameraId, CameraReportAction.GetVideoFiles)));

        using var document = JsonDocument.Parse(result);
        Assert.Contains(IntegrationTestHelpers.GetResult(document.RootElement).ValueKind,
            new[] { JsonValueKind.Array, JsonValueKind.Object });
    }

    private static void AssertOk(object result)
    {
        var json = Assert.IsType<string>(result);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Ok", IntegrationTestHelpers.GetResult(document.RootElement).GetProperty("status").GetString());
    }
}
