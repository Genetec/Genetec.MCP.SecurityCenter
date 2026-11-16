using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessRuleGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingAccessRule_ContainsName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_AccessRule_{Guid.NewGuid():N}";

        var createResult = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AccessRuleGetTool.Execute(sc.Manager, new AccessRuleGetTool.AccessRuleGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleGetTool.Execute(sc.Manager,
            new AccessRuleGetTool.AccessRuleGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
