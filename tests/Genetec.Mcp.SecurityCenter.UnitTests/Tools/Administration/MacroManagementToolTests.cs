using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class MacroManagementToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MacroManagementTool.Execute(TestHelpers.FakeManager(),
                new MacroManagementRequest(MacroAction.Create, Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MacroManagementTool.Execute(TestHelpers.FakeManager(),
                new MacroManagementRequest(MacroAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MacroAction.Get)]
    [InlineData(MacroAction.Update)]
    [InlineData(MacroAction.Delete)]
    [InlineData(MacroAction.Execute)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(MacroAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = (action is MacroAction.Update or MacroAction.Delete ? new MacroManagementRequest(action, Items: [new MacroManagementItem(Id: id, Body: action == MacroAction.Update ? new MacroUpdateProperties(Name: "Must not update") : null)]) : new MacroManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MacroManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action is MacroAction.Update or MacroAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(MacroAction.Get)]
    [InlineData(MacroAction.Update)]
    [InlineData(MacroAction.Execute)]
    public async Task TargetActions_MissingId_RejectsRequiredTarget(MacroAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MacroManagementTool.Execute(TestHelpers.FakeManager(),
            new MacroManagementRequest(action)));
        Assert.Equal(action is MacroAction.Update or MacroAction.Delete ? "items" : "id", error.ParamName);
    }
}
