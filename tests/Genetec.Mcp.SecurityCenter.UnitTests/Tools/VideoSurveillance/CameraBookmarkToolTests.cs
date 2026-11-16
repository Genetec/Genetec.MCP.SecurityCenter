using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraBookmarkToolTests
{
    [Theory]
    [InlineData(CameraBookmarkAction.Add)]
    [InlineData(CameraBookmarkAction.Edit)]
    [InlineData(CameraBookmarkAction.Delete)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(CameraBookmarkAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => CameraBookmarkTool.Execute(manager, new CameraBookmarkRequest(id, action, Description: "Rejected bookmark", BookmarkTime: DateTime.UtcNow)), "id", "UserGroup");
    }

    // ── Surface ─────────────────────────────────────────────────────

    [Fact]
    public void SupportedActions_ContainAllBookmarkOperations()
    {
        Assert.Equal(
            [
                CameraBookmarkAction.Add,
                CameraBookmarkAction.Edit,
                CameraBookmarkAction.Delete,
                CameraBookmarkAction.Report
            ],
            Enum.GetValues<CameraBookmarkAction>());
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(CameraBookmarkAction.Add)]
    [InlineData(CameraBookmarkAction.Edit)]
    public async Task DescriptionRequiredForAddAndEdit_ThrowsArgumentException(CameraBookmarkAction action)
    {
        var request = new CameraBookmarkRequest(
            Guid.NewGuid(),
            action,
            BookmarkTime: new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraBookmarkTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains("description", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_MissingBookmarkTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraBookmarkTool.Execute(
                TestHelpers.FakeManager(),
                new CameraBookmarkRequest(
                    Guid.NewGuid(),
                    CameraBookmarkAction.Edit,
                    Description: "Updated bookmark")));

        Assert.Contains("bookmark_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_MissingBookmarkTime_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraBookmarkTool.Execute(
                TestHelpers.FakeManager(),
                new CameraBookmarkRequest(Guid.NewGuid(), CameraBookmarkAction.Delete)));

        Assert.Contains("bookmark_time", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("time_start")]
    [InlineData("time_end")]
    public async Task Report_MissingTimeParameter_ThrowsArgumentException(string missingParameter)
    {
        var request = new CameraBookmarkRequest(
            Guid.NewGuid(),
            CameraBookmarkAction.Report,
            TimeStart: missingParameter == "time_start" ? null : new DateTime(2026, 8, 20, 14, 0, 0, DateTimeKind.Utc),
            TimeEnd: missingParameter == "time_end" ? null : new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraBookmarkTool.Execute(TestHelpers.FakeManager(), request));

        Assert.Contains(missingParameter, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
