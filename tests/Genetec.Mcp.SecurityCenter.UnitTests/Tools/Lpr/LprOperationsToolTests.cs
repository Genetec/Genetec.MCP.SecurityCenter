using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class LprOperationsToolTests
{


    [Theory]
    [InlineData(LprOperationsAction.TriggerRead)]
    [InlineData(LprOperationsAction.TriggerPastReadMatching)]
    public async Task Operation_WrongEntityType_RejectsBeforeAction(LprOperationsAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => LprOperationsTool.Execute(TestHelpers.FakeManager(handler),
            new LprOperationsRequest(action, id)));
        Assert.Equal("unit_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public void Request_ProductionBinding_DescribesActionSpecificTarget()
    {
        var id = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<LprOperationsRequest>(
            JsonSerializer.Serialize(new { action = "trigger_read", unit_id = id }), NumericStringConverter.ToolOptions)!;
        Assert.Equal(id, request.UnitId);
        Assert.Equal(LprOperationsAction.TriggerRead, request.Action);
    }
}
