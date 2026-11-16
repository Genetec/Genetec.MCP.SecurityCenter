using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class VideoUnitGetToolTests
{
    [Fact]
    public void Request_IdIsRequiredGuid()
    {
        TestHelpers.AssertRequiredProperty<VideoUnitGetTool.VideoUnitGetRequest>(nameof(VideoUnitGetTool.VideoUnitGetRequest.Id));
        Assert.Equal(typeof(Guid), typeof(VideoUnitGetTool.VideoUnitGetRequest)
            .GetProperty(nameof(VideoUnitGetTool.VideoUnitGetRequest.Id))!.PropertyType);
    }

    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<VideoUnitGetTool>("video_unit_get");

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<VideoUnitGetTool.VideoUnitGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VideoUnitGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("VideoUnit", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
