using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.VideoSurveillance;

public class StreamConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
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
    public async Task RelatedEntityTargets_RejectWrongEntityTypeAndPreserveEntities(StreamConfigAction action)
    {
        var primaryId = (await GetH264StreamAsync()).Id;
        await IntegrationTestHelpers.AssertWrongRelatedEntityTypeAsync(sc, primaryId, "Stream", relatedId => StreamConfigurationTool.Execute(sc.Manager, new StreamConfigRequest(primaryId, action, ScheduleId: relatedId, BitRate: 512, ConstantBitRate: true, FrameRate: 15, DynamicFrameRate: true, Width: 640, Height: 480, Interval: 1, IntervalMeasure: StreamIntervalMeasure.Seconds, RecordingFrameInterval: StreamRecordingFrameInterval.RecordAllFrames)), "schedule_id", "UserGroup");
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
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(StreamConfigAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => StreamConfigurationTool.Execute(sc.Manager, new StreamConfigRequest(id, action, BitRate: 512, ConstantBitRate: true, FrameRate: 15, DynamicFrameRate: true, Width: 640, Height: 480, Interval: 1, IntervalMeasure: StreamIntervalMeasure.Seconds, RecordingFrameInterval: StreamRecordingFrameInterval.RecordAllFrames)), "id", "UserGroup");
    }

    public static IEnumerable<object[]> IntervalMeasures()
        => Enum.GetValues<StreamIntervalMeasure>().Select(value => new object[] { value });

    public static IEnumerable<object[]> RecordingIntervals()
        => Enum.GetValues<StreamRecordingFrameInterval>().Select(value => new object[] { value });

    [Fact]
    public async Task SetBitRate_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetBitRate, BitRate: stream.BitRate));
        Assert.Equal(stream.BitRate, (await GetH264StreamAsync(stream.Id)).BitRate);
    }

    [Fact]
    public async Task SetConstantBitRate_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetConstantBitRate, ConstantBitRate: stream.ConstantBitRate));
        Assert.Equal(stream.ConstantBitRate, (await GetH264StreamAsync(stream.Id)).ConstantBitRate);
    }

    [Fact]
    public async Task SetFrameRate_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetFrameRate, FrameRate: stream.FrameRate));
        Assert.Equal(stream.FrameRate, (await GetH264StreamAsync(stream.Id)).FrameRate);
    }

    [Fact]
    public async Task SetDynamicFrameRate_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetDynamicFrameRate, DynamicFrameRate: stream.DynamicFrameRate));
        Assert.Equal(stream.DynamicFrameRate, (await GetH264StreamAsync(stream.Id)).DynamicFrameRate);
    }

    [Fact]
    public async Task SetDynamicFrameRateMinimum_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetDynamicFrameRateMin, FrameRate: stream.DynamicFrameRateMinimum));
        Assert.Equal(stream.DynamicFrameRateMinimum, (await GetH264StreamAsync(stream.Id)).DynamicFrameRateMinimum);
    }

    [Fact]
    public async Task SetResolution_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetResolution, Width: stream.Width, Height: stream.Height));
        var updated = await GetH264StreamAsync(stream.Id);
        Assert.Equal(stream.Width, updated.Width);
        Assert.Equal(stream.Height, updated.Height);
    }

    [Fact]
    public async Task SetKeyFrameInterval_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetKeyFrameInterval,
            Interval: stream.KeyFrameInterval, IntervalMeasure: stream.KeyFrameMeasure));
        var updated = await GetH264StreamAsync(stream.Id);
        Assert.Equal(stream.KeyFrameInterval, updated.KeyFrameInterval);
        Assert.Equal(stream.KeyFrameMeasure, updated.KeyFrameMeasure);
    }

    [Fact]
    public async Task SetRecordingFrameInterval_H264Stream_PersistsCurrentValue()
    {
        var stream = await GetH264StreamAsync();
        AssertOk(await Execute(stream, StreamConfigAction.SetRecordingFrameInterval,
            RecordingFrameInterval: stream.RecordingFrameInterval));
        Assert.Equal(stream.RecordingFrameInterval, (await GetH264StreamAsync(stream.Id)).RecordingFrameInterval);
    }

    [Theory]
    [MemberData(nameof(IntervalMeasures))]
    public async Task SetKeyFrameInterval_AllMeasureValues_Persists(StreamIntervalMeasure measure)
    {
        var stream = await GetH264StreamAsync();
        var interval = measure == StreamIntervalMeasure.Seconds ? 1 : stream.KeyFrameInterval;
        try
        {
            AssertOk(await Execute(stream, StreamConfigAction.SetKeyFrameInterval,
                Interval: interval, IntervalMeasure: measure));
            var updated = await GetH264StreamAsync(stream.Id);
            Assert.Equal(measure, updated.KeyFrameMeasure);
            Assert.Equal(interval, updated.KeyFrameInterval);
        }
        finally
        {
            await Execute(stream, StreamConfigAction.SetKeyFrameInterval,
                Interval: stream.KeyFrameInterval, IntervalMeasure: stream.KeyFrameMeasure);
        }
    }

    [Theory]
    [MemberData(nameof(RecordingIntervals))]
    public async Task SetRecordingFrameInterval_AllEnumValues_Persists(
        StreamRecordingFrameInterval interval)
    {
        var stream = await GetH264StreamAsync();
        try
        {
            AssertOk(await Execute(stream, StreamConfigAction.SetRecordingFrameInterval,
                RecordingFrameInterval: interval));
            Assert.Equal(interval, (await GetH264StreamAsync(stream.Id)).RecordingFrameInterval);
        }
        finally
        {
            await Execute(stream, StreamConfigAction.SetRecordingFrameInterval,
                RecordingFrameInterval: stream.RecordingFrameInterval);
        }
    }

    private async Task<object> Execute(
        StreamSnapshot stream,
        StreamConfigAction action,
        int? BitRate = null,
        bool? ConstantBitRate = null,
        double? FrameRate = null,
        bool? DynamicFrameRate = null,
        int? Width = null,
        int? Height = null,
        int? Interval = null,
        StreamIntervalMeasure? IntervalMeasure = null,
        StreamRecordingFrameInterval? RecordingFrameInterval = null)
        => await StreamConfigurationTool.Execute(sc.Manager,
            new StreamConfigRequest(stream.Id, action, stream.ScheduleId, BitRate, ConstantBitRate,
                FrameRate, DynamicFrameRate, Width, Height, Interval, IntervalMeasure, RecordingFrameInterval));

    private async Task<StreamSnapshot> GetH264StreamAsync(Guid? streamId = null)
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        if (streamId.HasValue)
            return await ReadStreamAsync(streamId.Value);

        var cameraIds = await IntegrationTestHelpers.GetVideoEncoderCameraIdsAsync(sc, maximumCameraCount: 10);
        foreach (var cameraId in cameraIds)
        {
            var cameraJson = await sc.Manager.GetClient().GetEntity(cameraId.ToString());
            using var camera = JsonDocument.Parse(cameraJson);
            if (!IntegrationTestHelpers.GetResult(camera.RootElement).TryGetProperty("Streams", out var streams) ||
                streams.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var streamValue in streams.EnumerateArray())
            {
                if (!streamValue.TryGetGuid(out var candidateId))
                    continue;

                var candidate = await ReadStreamAsync(candidateId);
                if (candidate.Algorithm.Equals("H264", StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        throw new Xunit.Sdk.XunitException("No H264 stream with a compression configuration was found.");
    }

    private async Task<StreamSnapshot> ReadStreamAsync(Guid streamId)
    {
        var streamJson = await sc.Manager.GetClient().GetEntity(streamId.ToString());
        using var stream = JsonDocument.Parse(streamJson);
        var root = IntegrationTestHelpers.GetResult(stream.RootElement);
        var compression = root.GetProperty("VideoCompressions")[0];
        var recordingText = compression.GetProperty("RecordingFrameInterval").GetString();
        Assert.True(Enum.TryParse<StreamRecordingFrameInterval>(recordingText, out var recordingInterval),
            $"Unknown recording frame interval '{recordingText}'.");

        var keyFrameMeasureText = compression.GetProperty("KeyframeIntervalMeasure").GetString();
        var keyFrameMeasure = Enum.TryParse<StreamIntervalMeasure>(keyFrameMeasureText, out var parsedKeyFrameMeasure)
            ? parsedKeyFrameMeasure
            : StreamIntervalMeasure.Images;

        return new StreamSnapshot(
            streamId,
            compression.GetProperty("Schedule").GetGuid(),
            root.GetProperty("VideoCompressionAlgorithm").GetString() ?? string.Empty,
            compression.GetProperty("BitRate").GetInt32(),
            root.GetProperty("ConstantBitRate").GetBoolean(),
            compression.GetProperty("FrameRate").GetDouble(),
            compression.GetProperty("DynamicFrameRate").GetBoolean(),
            compression.GetProperty("DynamicFrameRateMinimumFrameRate").GetDouble(),
            compression.GetProperty("ResolutionX").GetInt32(),
            compression.GetProperty("ResolutionY").GetInt32(),
            compression.GetProperty("KeyframeInterval").GetInt32(),
            keyFrameMeasure,
            recordingInterval);
    }

    private static IEnumerable<Guid> ExtractGuids(string json)
    {
        using var document = JsonDocument.Parse(json);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement).ValueKind == JsonValueKind.Array
            ? IntegrationTestHelpers.GetResult(document.RootElement)
            : IntegrationTestHelpers.GetResult(document.RootElement).GetProperty("Body");

        foreach (var row in rows.EnumerateArray())
        {
            if (row.TryGetProperty("Guid", out var guid) && guid.TryGetGuid(out var value))
                yield return value;
        }
    }

    private static void AssertOk(object result)
    {
        Assert.Contains("\"status\":\"Ok\"", (string)result);
    }

    private sealed record StreamSnapshot(
        Guid Id,
        Guid ScheduleId,
        string Algorithm,
        int BitRate,
        bool ConstantBitRate,
        double FrameRate,
        bool DynamicFrameRate,
        double DynamicFrameRateMinimum,
        int Width,
        int Height,
        int KeyFrameInterval,
        StreamIntervalMeasure KeyFrameMeasure,
        StreamRecordingFrameInterval RecordingFrameInterval);
}
