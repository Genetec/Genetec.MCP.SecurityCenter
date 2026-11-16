using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class OvertimeRuleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task List_ReturnsValidJson()
    {
        RequireSecurityCenter();

        var result = Assert.IsType<string>(await OvertimeRuleManagementTool.Execute(
            sc.Manager, new OvertimeRuleManagementRequest(OvertimeRuleAction.List)));

        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [Fact]
    public async Task Get_AndUpdate_UnknownRule_ReachesSecurityCenter()
    {
        RequireSecurityCenter();
        var id = Guid.NewGuid();
        var getException = await Record.ExceptionAsync(() => OvertimeRuleManagementTool.Execute(sc.Manager,
            new OvertimeRuleManagementRequest(OvertimeRuleAction.Get, Id: id)));
        var updateException = await Record.ExceptionAsync(() => OvertimeRuleManagementTool.Execute(sc.Manager,
            new OvertimeRuleManagementRequest(OvertimeRuleAction.Update, Items: [new OvertimeRuleManagementItem(Id: id, Body: new OvertimeRuleUpdateProperties(Name: "MCP_Overtime"))])));

        Assert.True(getException is null or not ArgumentException, getException?.ToString());
        Assert.True(updateException is null or not ArgumentException, updateException?.ToString());
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    [Theory]
    [InlineData(OvertimeRuleAction.Get)]
    [InlineData(OvertimeRuleAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(OvertimeRuleAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => OvertimeRuleManagementTool.Execute(sc.Manager,
            (action is OvertimeRuleAction.Update ? new OvertimeRuleManagementRequest(action, Items: [new OvertimeRuleManagementItem(Id: id, Body: action == OvertimeRuleAction.Update ? new OvertimeRuleUpdateProperties(Name: "Must not update") : null)]) : new OvertimeRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is OvertimeRuleAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
