using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class VisitorGetToolTests
{
    [Fact]
    public void Request_IdIsRequired()
        => TestHelpers.AssertRequiredProperty<VisitorGetTool.VisitorGetRequest>(nameof(VisitorGetTool.VisitorGetRequest.Id));

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<VisitorGetTool.VisitorGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("Visitor", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
