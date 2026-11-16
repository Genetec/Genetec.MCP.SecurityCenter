using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class OvertimeRuleManagementToolTests
{
    [Fact]
    public void SupportedActions_ContainOnlySupportedOperations()
    {
        Assert.Equal(
            [
                OvertimeRuleAction.Get,
                OvertimeRuleAction.List,
                OvertimeRuleAction.Update
            ],
            Enum.GetValues<OvertimeRuleAction>());
    }

    [Theory]
    [InlineData(OvertimeRuleAction.Get, "get")]
    [InlineData(OvertimeRuleAction.Update, "update")]
    public async Task MissingId_ThrowsArgumentException(OvertimeRuleAction action, string actionName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            OvertimeRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new OvertimeRuleManagementRequest(action)));

        Assert.Contains(action is OvertimeRuleAction.Update ? "items" : "id", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingBodyAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            OvertimeRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new OvertimeRuleManagementRequest(OvertimeRuleAction.Update, Items: [new OvertimeRuleManagementItem(Id: Guid.NewGuid())])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Update_EmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            OvertimeRuleManagementTool.Execute(TestHelpers.FakeManager(),
                new OvertimeRuleManagementRequest(OvertimeRuleAction.Update, Items: [new OvertimeRuleManagementItem(Name: name, Id: Guid.NewGuid())])));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OvertimeRuleAction.Get)]
    [InlineData(OvertimeRuleAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(OvertimeRuleAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => OvertimeRuleManagementTool.Execute(TestHelpers.FakeManager(handler),
            (action is OvertimeRuleAction.Update ? new OvertimeRuleManagementRequest(action, Items: [new OvertimeRuleManagementItem(Id: id, Body: action == OvertimeRuleAction.Update ? new OvertimeRuleUpdateProperties(Name: "Must not update") : null)]) : new OvertimeRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is OvertimeRuleAction.Update ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
