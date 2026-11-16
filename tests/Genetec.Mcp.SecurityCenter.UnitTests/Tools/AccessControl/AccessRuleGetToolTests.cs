using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class AccessRuleGetToolTests
{
    [Fact]
    public void Request_IdIsRequiredGuid()
    {
        TestHelpers.AssertRequiredProperty<AccessRuleGetTool.AccessRuleGetRequest>(nameof(AccessRuleGetTool.AccessRuleGetRequest.Id));
        Assert.Equal(typeof(Guid), typeof(AccessRuleGetTool.AccessRuleGetRequest)
            .GetProperty(nameof(AccessRuleGetTool.AccessRuleGetRequest.Id))!.PropertyType);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<AccessRuleGetTool.AccessRuleGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("AccessRule", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
