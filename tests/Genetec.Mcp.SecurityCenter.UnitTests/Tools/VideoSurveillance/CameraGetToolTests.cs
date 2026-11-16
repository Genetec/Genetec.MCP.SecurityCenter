using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.VideoSurveillance;

public class CameraGetToolTests
{
    [Fact]
    public void Request_IdIsRequiredGuid()
    {
        TestHelpers.AssertRequiredProperty<CameraGetTool.CameraGetRequest>(nameof(CameraGetTool.CameraGetRequest.Id));
        Assert.Equal(typeof(Guid), typeof(CameraGetTool.CameraGetRequest)
            .GetProperty(nameof(CameraGetTool.CameraGetRequest.Id))!.PropertyType);
    }

    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<CameraGetTool>("camera_get");

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<CameraGetTool.CameraGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CameraGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("Camera", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
