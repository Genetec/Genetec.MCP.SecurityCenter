using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class LprOperationsToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(LprOperationsAction.TriggerRead, "Device", "EntityTypeFilter.Add(Device,BA==)")]
    [InlineData(LprOperationsAction.TriggerPastReadMatching, "Role", "EntityTypeFilter.Add(Role,CA==)")]
    public async Task Operation_DocumentedTarget_ReturnsSdkSuccess(LprOperationsAction action, string entityType, string filter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var targets = await sc.Manager.GetClient().RunPagedReport("EntityConfiguration", filter, 50, 1);
        using var document = IntegrationTestHelpers.ParseJson(targets);
        var rows = IntegrationTestHelpers.GetResult(document.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, rows.ValueKind);
        Assert.True(rows.GetArrayLength() > 0, $"No configured {entityType} target for {action}.");
        var ids = IntegrationTestHelpers.ExtractGuids(rows.GetRawText());
        Assert.NotEmpty(ids);
        var id = ids[0];
        var name = await IntegrationTestHelpers.GetEntityNameAsync(sc, id, entityType);

        var result = await LprOperationsTool.Execute(sc.Manager, new LprOperationsRequest(action, id));

        using var response = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(result));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, response.RootElement.ValueKind);
        Assert.Empty(response.RootElement.EnumerateArray());
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, entityType, name);
    }

    [Theory]
    [InlineData(LprOperationsAction.TriggerRead)]
    [InlineData(LprOperationsAction.TriggerPastReadMatching)]
    public async Task Operation_WrongEntityType_RejectsBeforeAction(LprOperationsAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => LprOperationsTool.Execute(sc.Manager,
            new LprOperationsRequest(action, id)));
        Assert.Equal("unit_id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
