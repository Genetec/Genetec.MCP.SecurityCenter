using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class CameraBookmarkToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(CameraBookmarkAction.Add)]
    [InlineData(CameraBookmarkAction.Edit)]
    [InlineData(CameraBookmarkAction.Delete)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(CameraBookmarkAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => CameraBookmarkTool.Execute(sc.Manager, new CameraBookmarkRequest(id, action, Description: "Rejected bookmark", BookmarkTime: DateTime.UtcNow)), "id", "UserGroup");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task AddEditDelete_ReadsBackThroughReport()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();
        var bookmarkTime = DateTime.UtcNow;
        var originalDescription = $"MCP_Bookmark_{Guid.NewGuid():N}" + @" %2C & # + , (literal) @site\item";
        var updatedDescription = $"{originalDescription}_Updated %2C & # +";
        var added = false;

        try
        {
            var addResult = await CameraBookmarkTool.Execute(sc.Manager,
                new CameraBookmarkRequest(
                    cameraId,
                    CameraBookmarkAction.Add,
                    Description: originalDescription,
                    BookmarkTime: bookmarkTime));
            Assert.IsType<string>(addResult);
            added = true;
            await Task.Delay(1000);

            var addedReport = await RunBookmarkReportAsync(cameraId);
            Assert.True(ContainsDescription(addedReport, originalDescription),
                "The added bookmark was not returned by the Bookmark report.");

            var editResult = await CameraBookmarkTool.Execute(sc.Manager,
                new CameraBookmarkRequest(
                    cameraId,
                    CameraBookmarkAction.Edit,
                    Description: updatedDescription,
                    BookmarkTime: bookmarkTime));
            Assert.IsType<string>(editResult);
            await Task.Delay(1000);

            var editedReport = await RunBookmarkReportAsync(cameraId);
            Assert.True(ContainsDescription(editedReport, updatedDescription),
                "The edited bookmark description was not returned by the Bookmark report.");
            Assert.False(ContainsExactDescription(editedReport, originalDescription),
                "The original bookmark description remained after edit.");

            var deleteResult = await CameraBookmarkTool.Execute(sc.Manager,
                new CameraBookmarkRequest(
                    cameraId,
                    CameraBookmarkAction.Delete,
                    BookmarkTime: bookmarkTime));
            Assert.IsType<string>(deleteResult);
            await Task.Delay(1000);

            var deletedReport = await RunBookmarkReportAsync(cameraId);
            Assert.False(ContainsExactDescription(deletedReport, updatedDescription),
                "The deleted bookmark was still returned by the Bookmark report.");
        }
        finally
        {
            if (added)
            {
                try
                {
                    await CameraBookmarkTool.Execute(sc.Manager,
                        new CameraBookmarkRequest(
                            cameraId,
                            CameraBookmarkAction.Delete,
                            BookmarkTime: bookmarkTime));
                }
                catch
                {
                    // Best-effort cleanup; the assertion above reports the operation failure.
                }
            }
        }
    }

    [Fact]
    public async Task Report_ValidCamera_ReturnsJson()
    {
        RequireSecurityCenter();
        var cameraId = await GetFirstCameraIdAsync();
        var now = DateTime.UtcNow;

        var result = await CameraBookmarkTool.Execute(sc.Manager,
            new CameraBookmarkRequest(
                cameraId,
                CameraBookmarkAction.Report,
                TimeStart: now.AddHours(-1),
                TimeEnd: now));

        var json = Assert.IsType<string>(result);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object);
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private Task<Guid> GetFirstCameraIdAsync()
        => IntegrationTestHelpers.GetFirstVideoEncoderCameraIdAsync(sc);

    private async Task<string> RunBookmarkReportAsync(Guid cameraId)
        => (string)await CameraBookmarkTool.Execute(sc.Manager,
            new CameraBookmarkRequest(
                cameraId,
                CameraBookmarkAction.Report,
                TimeStart: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                TimeEnd: new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

    private static bool ContainsDescription(string json, string description)
    {
        using var document = JsonDocument.Parse(json);
        var rows = GetRows(document.RootElement);

        return rows.Any(row =>
            row.TryGetProperty("Notes", out var notes) &&
            notes.ValueKind == JsonValueKind.String &&
            notes.GetString()!.Contains(description, StringComparison.Ordinal));
    }

    private static bool ContainsExactDescription(string json, string description)
    {
        using var document = JsonDocument.Parse(json);
        return GetRows(document.RootElement).Any(row =>
            row.TryGetProperty("Notes", out var notes) &&
            notes.ValueKind == JsonValueKind.String &&
            notes.GetString()!.EndsWith($": {description}", StringComparison.Ordinal));
    }

    private static IEnumerable<JsonElement> GetRows(JsonElement root)
        => root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array
                ? body.EnumerateArray()
                : [];
}
