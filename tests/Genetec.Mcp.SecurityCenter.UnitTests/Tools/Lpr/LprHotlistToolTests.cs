using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class LprHotlistToolTests
{
    [Theory]
    [InlineData(LprHotlistAction.Create)]
    [InlineData(LprHotlistAction.Delete)]
    public async Task Batch_NullItemInProductionRequest_RejectsBeforeSdk(LprHotlistAction action)
    {
        var json = "{\"action\":\"" + action.ToString().ToLowerInvariant() + "\",\"items\":[null]}";
        var request = JsonSerializer.Deserialize<LprHotlistManagementRequest>(
            json, NumericStringConverter.ToolOptions)!;
        var handler = new EntityMetadataTestHandler();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => LprHotlistTool.Execute(
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
            LprHotlistTool.Execute(TestHelpers.FakeManager(),
                new LprHotlistManagementRequest(LprHotlistAction.Create, Name: name)));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_BothNameAndItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprHotlistTool.Execute(TestHelpers.FakeManager(),
                new LprHotlistManagementRequest(LprHotlistAction.Create, Name: "Hotlist A", Items: [new LprHotlistBatchItem(Name: "Hotlist B")])));

        Assert.Contains("either", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprHotlistTool.Execute(TestHelpers.FakeManager(),
                new LprHotlistManagementRequest(LprHotlistAction.Get)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprHotlistTool.Execute(TestHelpers.FakeManager(),
                new LprHotlistManagementRequest(LprHotlistAction.Update, Name: "NewName")));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullUpdateAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            LprHotlistTool.Execute(TestHelpers.FakeManager(),
                new LprHotlistManagementRequest(LprHotlistAction.Update, Id: Guid.NewGuid())));

        Assert.Contains("update", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(LprHotlistAction.Get)]
    [InlineData(LprHotlistAction.Update)]
    [InlineData(LprHotlistAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectBeforeReadOrMutation(LprHotlistAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action switch
        {
            LprHotlistAction.Get => new LprHotlistManagementRequest(action, Id: id),
            LprHotlistAction.Update => new LprHotlistManagementRequest(action, Id: id,
                Update: new LprHotlistUpdateRequest(Name: "Should not update")),
            _ => new LprHotlistManagementRequest(action, Id: id)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => LprHotlistTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(action == LprHotlistAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }

}
