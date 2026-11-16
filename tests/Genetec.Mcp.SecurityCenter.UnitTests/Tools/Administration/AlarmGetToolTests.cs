using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;


namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AlarmGetToolTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    [InlineData("{\"id\":\"\"}")]
    [InlineData("{\"id\":\"not-a-guid\"}")]
    public void Request_MissingOrInvalidIdRejectedBeforeExecution(string json)
        => Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AlarmGetTool.AlarmGetRequest>(json, NumericStringConverter.ToolOptions));

    [Fact]
    public void Tool_UsesExpectedName()
        => TestHelpers.AssertToolName<AlarmGetTool>("alarm_get");

    [Fact]
    public async Task Get_WrongEntityType_RejectsTarget()
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var json = JsonSerializer.Serialize(new { id });
        var request = JsonSerializer.Deserialize<AlarmGetTool.AlarmGetRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AlarmGetTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal("id", error.ParamName);
        Assert.Contains("Alarm", error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
