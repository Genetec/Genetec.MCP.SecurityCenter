using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class PermitManagementToolTests
{
    [Theory]
    [InlineData(PermitAction.Create)]
    [InlineData(PermitAction.Delete)]
    public async Task Batch_NullItemInProductionRequest_RejectsBeforeSdk(PermitAction action)
    {
        var json = "{\"action\":\"" + action.ToString().ToLowerInvariant() + "\",\"items\":[null]}";
        var request = JsonSerializer.Deserialize<PermitManagementRequest>(
            json, NumericStringConverter.ToolOptions)!;
        var handler = new EntityMetadataTestHandler();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitManagementTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal("items", error.ParamName);
        Assert.Contains("index 0", error.Message);
        Assert.Empty(handler.Requests);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_MissingName_ThrowsArgumentException(string? name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitManagementRequest(PermitAction.Create, Name: name)));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_BothNameAndItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitManagementRequest(PermitAction.Create, Name: "Permit A", Items: [new PermitBatchItem(Name: "Permit B")])));

        Assert.Contains("either", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitManagementRequest(PermitAction.Update,
                    Body: new PermitUpdateProperties(Name: "Updated"))));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingBodyAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitManagementRequest(PermitAction.Update, Id: Guid.NewGuid())));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_MissingIdAndItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PermitManagementTool.Execute(TestHelpers.FakeManager(),
                new PermitManagementRequest(PermitAction.Delete)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PermitAction.Get)]
    [InlineData(PermitAction.Update)]
    [InlineData(PermitAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectBeforeReadOrMutation(PermitAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action switch
        {
            PermitAction.Get => new PermitManagementRequest(action, Id: id),
            PermitAction.Update => new PermitManagementRequest(action, Id: id,
                Body: new PermitUpdateProperties(Name: "Should not update")),
            _ => new PermitManagementRequest(action, Id: id)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitManagementTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(action == PermitAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }
}
