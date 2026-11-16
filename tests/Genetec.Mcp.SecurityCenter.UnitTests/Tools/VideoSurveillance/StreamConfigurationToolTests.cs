using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class StreamConfigurationToolTests
{
    [Theory]
    [InlineData(StreamConfigAction.SetBitRate)]
    [InlineData(StreamConfigAction.SetConstantBitRate)]
    [InlineData(StreamConfigAction.SetFrameRate)]
    [InlineData(StreamConfigAction.SetDynamicFrameRate)]
    [InlineData(StreamConfigAction.SetDynamicFrameRateMin)]
    [InlineData(StreamConfigAction.SetResolution)]
    [InlineData(StreamConfigAction.SetKeyFrameInterval)]
    [InlineData(StreamConfigAction.SetRecordingFrameInterval)]
    public async Task RelatedEntityTargets_RejectWrongEntityType(StreamConfigAction action)
    {
        await TestHelpers.AssertWrongRelatedEntityTypeAsync("Stream", (manager, primaryId, relatedId) => StreamConfigurationTool.Execute(manager, new StreamConfigRequest(primaryId, action, ScheduleId: relatedId, BitRate: 512, ConstantBitRate: true, FrameRate: 15, DynamicFrameRate: true, Width: 640, Height: 480, Interval: 1, IntervalMeasure: StreamIntervalMeasure.Seconds, RecordingFrameInterval: StreamRecordingFrameInterval.RecordAllFrames)), "schedule_id", "UserGroup");
    }

    [Theory]
    [InlineData(StreamConfigAction.SetBitRate)]
    [InlineData(StreamConfigAction.SetConstantBitRate)]
    [InlineData(StreamConfigAction.SetFrameRate)]
    [InlineData(StreamConfigAction.SetDynamicFrameRate)]
    [InlineData(StreamConfigAction.SetDynamicFrameRateMin)]
    [InlineData(StreamConfigAction.SetResolution)]
    [InlineData(StreamConfigAction.SetKeyFrameInterval)]
    [InlineData(StreamConfigAction.SetRecordingFrameInterval)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(StreamConfigAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => StreamConfigurationTool.Execute(manager, new StreamConfigRequest(id, action, BitRate: 512, ConstantBitRate: true, FrameRate: 15, DynamicFrameRate: true, Width: 640, Height: 480, Interval: 1, IntervalMeasure: StreamIntervalMeasure.Seconds, RecordingFrameInterval: StreamRecordingFrameInterval.RecordAllFrames)), "id", "UserGroup");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SetBitRate_NullBitRate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetBitRate, BitRate: null)));

        Assert.Contains("bit_rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetConstantBitRate_NullConstantBitRate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetConstantBitRate, ConstantBitRate: null)));

        Assert.Contains("constant_bit_rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetFrameRate_NullFrameRate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetFrameRate, FrameRate: null)));

        Assert.Contains("frame_rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetDynamicFrameRate_NullFrameRate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetDynamicFrameRate, DynamicFrameRate: null)));

        Assert.Contains("dynamic_frame_rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetDynamicFrameRateMin_NullFrameRate_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetDynamicFrameRateMin, FrameRate: null)));

        Assert.Contains("frame_rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetResolution_NullWidth_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetResolution, Width: null, Height: 1080)));

        Assert.Contains("width", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetResolution_NullHeight_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetResolution, Width: 1920, Height: null)));

        Assert.Contains("height", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetKeyFrameInterval_NullInterval_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetKeyFrameInterval, Interval: null)));

        Assert.Contains("interval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetKeyFrameInterval_NullMeasure_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetKeyFrameInterval, Interval: 10)));

        Assert.Contains("interval_measure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetRecordingFrameInterval_NullRecordingFrameInterval_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            StreamConfigurationTool.Execute(TestHelpers.FakeManager(),
                new StreamConfigRequest(Guid.NewGuid(), StreamConfigAction.SetRecordingFrameInterval, RecordingFrameInterval: null)));

        Assert.Contains("recording_frame_interval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
