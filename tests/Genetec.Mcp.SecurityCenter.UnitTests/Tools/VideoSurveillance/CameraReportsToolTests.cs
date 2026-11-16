using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraReportsToolTests
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => CameraReportsTool.Execute(manager, new CameraReportsRequest(id, CameraReportAction.GetMetadataStreams)), "id", "UserGroup");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task EventReport_MissingTimeStart_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraReportsTool.Execute(TestHelpers.FakeManager(),
                new CameraReportsRequest(Guid.NewGuid(), CameraReportAction.EventReport,
                    TimeEnd: "2024-01-01T00:00:00Z")));
        Assert.Contains("time_start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventReport_MissingTimeEnd_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CameraReportsTool.Execute(TestHelpers.FakeManager(),
                new CameraReportsRequest(Guid.NewGuid(), CameraReportAction.EventReport,
                    TimeStart: "2024-01-01T00:00:00Z")));
        Assert.Contains("time_end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
