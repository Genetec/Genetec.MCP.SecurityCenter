using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class PermitRuleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // Permit-rule creation and deletion are not exposed by the Security Center
    // Web SDK, so integration coverage is limited to its supported actions.

    [Fact]
    public async Task List_ReturnsJson()
    {
        RequireSecurityCenter();

        var result = await PermitRuleManagementTool.Execute(sc.Manager,
            new PermitRuleManagementRequest(PermitRuleAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Get_ExistingRule_ReturnsJson()
    {
        RequireSecurityCenter();

        var id = await GetFirstRuleIdAsync();
        var result = await PermitRuleManagementTool.Execute(sc.Manager,
            new PermitRuleManagementRequest(PermitRuleAction.Get, Id: id));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Update_ExistingRule_RestoresOriginalName()
    {
        RequireSecurityCenter();

        var id = await GetFirstRuleIdAsync();
        var originalName = await GetRuleNameAsync(id);
        var temporaryName = $"MCP_PermitRule_{Guid.NewGuid():N}";

        try
        {
            var result = await PermitRuleManagementTool.Execute(sc.Manager,
                new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Name: temporaryName, Id: id)]));

            Assert.IsType<string>(result);
            Assert.Equal(temporaryName, await GetRuleNameAsync(id));
        }
        finally
        {
            await PermitRuleManagementTool.Execute(sc.Manager,
                new PermitRuleManagementRequest(PermitRuleAction.Update, Items: [new PermitRuleManagementItem(Name: originalName, Id: id)]));
        }
    }

    private async Task<Guid> GetFirstRuleIdAsync()
    {
        var result = Assert.IsType<string>(await PermitRuleManagementTool.Execute(sc.Manager,
            new PermitRuleManagementRequest(PermitRuleAction.List)));
        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).ValueKind == System.Text.Json.JsonValueKind.Array &&
                    IntegrationTestHelpers.GetResult(document.RootElement).GetArrayLength() > 0,
            "No permit rules available.");

        return Guid.Parse(IntegrationTestHelpers.GetResult(document.RootElement)[0].GetProperty("Guid").GetString()!);
    }

    private async Task<string> GetRuleNameAsync(Guid id)
    {
        var result = Assert.IsType<string>(await PermitRuleManagementTool.Execute(sc.Manager,
            new PermitRuleManagementRequest(PermitRuleAction.Get, Id: id)));
        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("Name", out var name) &&
                    name.ValueKind == System.Text.Json.JsonValueKind.String,
            "The permit rule did not return a name.");
        return name.GetString()!;
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    [Theory]
    [InlineData(PermitRuleAction.Get)]
    [InlineData(PermitRuleAction.Update)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(PermitRuleAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => PermitRuleManagementTool.Execute(sc.Manager,
            (action is PermitRuleAction.Update ? new PermitRuleManagementRequest(action, Items: [new PermitRuleManagementItem(Id: id, Body: action == PermitRuleAction.Update ? new PermitRuleUpdateProperties(Name: "Must not update") : null)]) : new PermitRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is PermitRuleAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
