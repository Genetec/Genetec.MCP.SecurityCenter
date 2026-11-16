using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AccessRuleDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_AccessRule_{Guid.NewGuid():N}";
        var createResult = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await AccessRuleDeleteTool.Execute(sc.Manager,
            new AccessRuleDeleteRequest([created.Guid]));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var create1 = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem($"MCP_AccessRule_{Guid.NewGuid():N}")]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);

        var create2 = await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem($"MCP_AccessRule_{Guid.NewGuid():N}")]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created1.Guid);
        sc.TrackForCleanup(created2.Guid);

        var result = await AccessRuleDeleteTool.Execute(sc.Manager,
            new AccessRuleDeleteRequest([created1.Guid, created2.Guid]));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);

        var singleError = await Assert.ThrowsAsync<ArgumentException>(() => AccessRuleDeleteTool.Execute(sc.Manager,
            new AccessRuleDeleteRequest([wrongTarget.Id])));
        Assert.Equal("items", singleError.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var created = Assert.IsType<EntityCreateResult>(await AccessRuleCreateTool.Execute(sc.Manager,
            new AccessRuleCreateRequest([new AccessRuleItem($"MCP_AccessRule_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);

        var batch = await AccessRuleDeleteTool.Execute(sc.Manager, new AccessRuleDeleteRequest([created.Guid, wrongTarget.Id]));
        IntegrationTestHelpers.AssertPartialBatchResult(batch, created.Guid, wrongTarget.Id);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
