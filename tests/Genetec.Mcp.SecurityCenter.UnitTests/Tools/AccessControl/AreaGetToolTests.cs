using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AreaGetToolTests
{
    [Fact]
    public void Request_IdIsRequiredGuid()
    {
        TestHelpers.AssertRequiredProperty<AreaGetTool.AreaGetRequest>(nameof(AreaGetTool.AreaGetRequest.Id));
        Assert.Equal(typeof(Guid), typeof(AreaGetTool.AreaGetRequest)
            .GetProperty(nameof(AreaGetTool.AreaGetRequest.Id))!.PropertyType);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<AreaGetTool.AreaGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AreaGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("Area", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
