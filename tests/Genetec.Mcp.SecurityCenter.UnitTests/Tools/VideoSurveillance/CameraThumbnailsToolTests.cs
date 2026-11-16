using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraThumbnailsToolTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RecordedInterval_UsesStartThenEndForEveryCamera(int cameraCount)
    {
        var cameraIds = Enumerable.Range(0, cameraCount).Select(_ => Guid.NewGuid()).ToList();
        const string start = "2026-10-27T16:23:25Z";
        const string end = "2026-10-27T16:23:35Z";
        var requestJson = JsonSerializer.Serialize(new
        {
            camera_ids = cameraIds, time_start = start, time_end = end, width = 320, count = 3
        });
        var request = JsonSerializer.Deserialize<CameraThumbnailsRequest>(requestJson, NumericStringConverter.ToolOptions)!;
        var handler = new ToolTestHandler();

        await CameraThumbnailsTool.Execute(TestHelpers.FakeManager(handler), request);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("/WebSdk/report/Thumbnail", uri.AbsolutePath);
        var expectedQuery = string.Join(",", cameraIds.Select(id =>
            $"AddTimestampRange({id},{start},{end},3,320)"));
        Assert.Equal($"?q={expectedQuery}", Uri.UnescapeDataString(uri.Query));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void CameraIds_RequireAtLeastOneGuid(int count, bool expectedValid)
    {
        var request = new CameraThumbnailsRequest(
            Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList(),
            "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z");
        Assert.Equal(expectedValid, Validator.TryValidateObject(
            request, new ValidationContext(request), [], validateAllProperties: true));
    }

    [Fact]
    public void CameraIds_InvalidGuid_IsRejectedDuringBinding()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CameraThumbnailsRequest>(
            """{"CameraIds":["invalid"],"TimeStart":"2026-10-23T00:00:00Z","TimeEnd":"2026-10-23T00:01:00Z"}"""));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(640, 0)]
    [InlineData(640, -1)]
    public async Task ImageDimensionsAndCounts_RejectOutsideBounds(int width, int count)
    {
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<CameraThumbnailsRequest>(JsonSerializer.Serialize(new
        {
            camera_ids = new[] { Guid.NewGuid() }, time_start = "2026-10-23T00:00:00Z",
            time_end = "2026-10-23T00:01:00Z", width, count
        }), NumericStringConverter.ToolOptions));
        var handler = new ToolTestHandler();
        await Assert.ThrowsAsync<ArgumentException>(() => CameraThumbnailsTool.Execute(TestHelpers.FakeManager(handler),
            new CameraThumbnailsRequest([Guid.NewGuid()], "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z", width, count)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Discovery_AdvertisesCameraLimitAndPositiveWidthAndCount()
    {
        using var discovery = JsonDocument.Parse(JsonSerializer.Serialize(SearchToolsTool.Execute(
            new SearchToolsRequest(Query: "camera_thumbnails", Detail: ToolDetailLevel.Full)), NumericStringConverter.ToolOptions));
        var tool = Assert.Single(discovery.RootElement.EnumerateArray(), item => item.GetProperty("name").GetString() == "camera_thumbnails");
        var fields = Assert.Single(tool.GetProperty("parameters").EnumerateArray()).GetProperty("fields");
        Assert.Equal(10, Assert.Single(fields.EnumerateArray(), field => field.GetProperty("name").GetString() == "camera_ids")
            .GetProperty("max_length").GetInt32());
        foreach (var name in new[] { "width", "count" })
        {
            var field = Assert.Single(fields.EnumerateArray(), item => item.GetProperty("name").GetString() == name);
            Assert.Equal(1, field.GetProperty("min").GetInt32());
            Assert.Equal(int.MaxValue, field.GetProperty("max").GetInt32());
        }
    }

    [Theory]
    [InlineData(6, 10, false)]
    [InlineData(1, 51, false)]
    [InlineData(10, int.MaxValue, false)]
    [InlineData(1, 1, true)]
    public async Task ExpandedBudgetAndTimeOrder_RejectBeforeSdk(int cameras, int count, bool reversed)
    {
        var handler = new ToolTestHandler();
        var request = new CameraThumbnailsRequest(Enumerable.Range(0, cameras).Select(_ => Guid.NewGuid()).ToList(),
            reversed ? "2026-10-23T00:02:00Z" : "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z", Count: count);
        await Assert.ThrowsAsync<ArgumentException>(() => CameraThumbnailsTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(1, 11, 1281)]
    [InlineData(1, 50, int.MaxValue)]
    [InlineData(2, 25, 1920)]
    public async Task PositiveWidthAndAggregateCount_BindAndForwardWithoutExtraCeilings(int cameras, int count, int width)
    {
        var cameraIds = Enumerable.Range(0, cameras).Select(_ => Guid.NewGuid()).ToList();
        const string timestamp = "2026-10-23T00:00:00Z";
        var request = JsonSerializer.Deserialize<CameraThumbnailsRequest>(JsonSerializer.Serialize(new
        {
            camera_ids = cameraIds, time_start = timestamp, time_end = timestamp, width, count
        }), NumericStringConverter.ToolOptions)!;
        var handler = new ToolTestHandler();

        await CameraThumbnailsTool.Execute(TestHelpers.FakeManager(handler), request);

        var expected = string.Join(",", cameraIds.Select(id => $"AddTimestampRange({id},{timestamp},{timestamp},{count},{width})"));
        Assert.Equal($"?q={expected}", Uri.UnescapeDataString(Assert.Single(handler.Requests).Query));
    }

    [Fact]
    public async Task CancelledInvocation_RejectsBeforeSdk()
    {
        var handler = new ToolTestHandler();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CameraThumbnailsTool.Execute(TestHelpers.FakeManager(handler),
            new CameraThumbnailsRequest([Guid.NewGuid()], "2026-10-23T00:00:00Z", "2026-10-23T00:01:00Z"), new CancellationToken(true)));
        Assert.Empty(handler.Requests);
    }
}
