using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesOtherEntity(bool invalidFirst)
    {
        await IntegrationTestHelpers.AssertDeleteTypeGuardAsync(sc,
            async () => Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
                new CardholderCreateRequest([new CardholderItem($"MCP_CardholderTypeGuard_{Guid.NewGuid():N}")]))),
            ids => CardholderDeleteTool.Execute(sc.Manager, new CardholderDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Cardholder_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderDeleteTool.Execute(sc.Manager,
            new CardholderDeleteRequest([created.Guid]));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, [created.Guid]);
    }

    [Fact]
    public async Task Delete_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var create1 = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Cardholder_{Guid.NewGuid():N}")]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);

        var create2 = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Cardholder_{Guid.NewGuid():N}")]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created1.Guid);
        sc.TrackForCleanup(created2.Guid);

        var result = await CardholderDeleteTool.Execute(sc.Manager,
            new CardholderDeleteRequest([created1.Guid, created2.Guid]));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, [created1.Guid, created2.Guid]);
    }
}
