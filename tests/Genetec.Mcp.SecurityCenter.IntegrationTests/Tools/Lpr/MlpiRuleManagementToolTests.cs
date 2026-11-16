using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class MlpiRuleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────
    // MLPI rule creation and location mutation are not exposed by the WebSDK.
    // Tests use pre-existing entities for the operations the WebSDK does expose.

    [Fact]
    public async Task List_ReturnsJson()
    {
        RequireSecurityCenter();

        var result = await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Get_ExistingRule_ReturnsJson()
    {
        RequireSecurityCenter();

        var listResult = await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.List));
        var doc = System.Text.Json.JsonDocument.Parse((string)listResult);
        Assert.True(IntegrationTestHelpers.GetResult(doc.RootElement).ValueKind == System.Text.Json.JsonValueKind.Array && IntegrationTestHelpers.GetResult(doc.RootElement).GetArrayLength() > 0,
            "No MLPI rules available.");

        var id = Guid.Parse(IntegrationTestHelpers.GetResult(doc.RootElement)[0].GetProperty("Guid").GetString()!);
        var result = await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.Get, Id: id));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task GetLocations_ExistingRule_ReturnsResult()
    {
        RequireSecurityCenter();

        var listResult = await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.List));
        var doc = System.Text.Json.JsonDocument.Parse((string)listResult);
        Assert.True(IntegrationTestHelpers.GetResult(doc.RootElement).ValueKind == System.Text.Json.JsonValueKind.Array && IntegrationTestHelpers.GetResult(doc.RootElement).GetArrayLength() > 0,
            "No MLPI rules available.");

        var id = Guid.Parse(IntegrationTestHelpers.GetResult(doc.RootElement)[0].GetProperty("Guid").GetString()!);
        var result = await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.GetLocations, Id: id));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Update_ExistingRule_RestoresOriginalName()
    {
        RequireSecurityCenter();

        var id = await GetFirstRuleIdAsync();
        var original = await GetRuleNameAsync(id);
        var temporaryName = $"MCP_Mlpi_{Guid.NewGuid():N}";

        try
        {
            var updateResult = await MlpiRuleManagementTool.Execute(sc.Manager,
                new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Name: temporaryName, Id: id)]));

            Assert.IsType<string>(updateResult);
            Assert.Equal(temporaryName, await GetRuleNameAsync(id));
        }
        finally
        {
            await MlpiRuleManagementTool.Execute(sc.Manager,
                new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Name: original, Id: id)]));
        }
    }

    private async Task<Guid> GetFirstRuleIdAsync()
    {
        var listResult = Assert.IsType<string>(await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.List)));
        using var document = IntegrationTestHelpers.ParseJson(listResult);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).ValueKind == System.Text.Json.JsonValueKind.Array &&
                    IntegrationTestHelpers.GetResult(document.RootElement).GetArrayLength() > 0,
            "No MLPI rules available.");

        return Guid.Parse(IntegrationTestHelpers.GetResult(document.RootElement)[0].GetProperty("Guid").GetString()!);
    }

    private async Task<string> GetRuleNameAsync(Guid id)
    {
        var result = Assert.IsType<string>(await MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.Get, Id: id)));
        using var document = IntegrationTestHelpers.ParseJson(result);
        Assert.True(IntegrationTestHelpers.GetResult(document.RootElement).TryGetProperty("Name", out var name) &&
                    name.ValueKind == System.Text.Json.JsonValueKind.String,
            "The MLPI rule did not return a name.");
        return name.GetString()!;
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    [Theory]
    [InlineData(MlpiRuleAction.Get)]
    [InlineData(MlpiRuleAction.Update)]
    [InlineData(MlpiRuleAction.GetLocations)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(MlpiRuleAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MlpiRuleManagementTool.Execute(sc.Manager,
            (action is MlpiRuleAction.Update ? new MlpiRuleManagementRequest(action, Items: [new MlpiRuleManagementItem(Id: id, Body: action == MlpiRuleAction.Update ? new MlpiRuleUpdateProperties(Name: "Must not update") : null)]) : new MlpiRuleManagementRequest(action, Id: id))));
        Assert.Equal(action is MlpiRuleAction.Update ? "items" : "id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Fact]
    public async Task Update_WrongLprManagerType_RejectsLinkedTarget()
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = await GetFirstRuleIdAsync();
        var originalName = await GetRuleNameAsync(id);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MlpiRuleManagementTool.Execute(sc.Manager,
            new MlpiRuleManagementRequest(MlpiRuleAction.Update, Items: [new MlpiRuleManagementItem(Id: id, Body: new MlpiRuleUpdateProperties(LprManager: wrongId))])));
        Assert.Equal("items.body.lpr_manager", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "LprMlpiRule", originalName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }
}
