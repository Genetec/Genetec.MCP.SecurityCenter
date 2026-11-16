using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesOtherEntity(bool invalidFirst)
    {
        await IntegrationTestHelpers.AssertDeleteTypeGuardAsync(sc,
            async () => Assert.IsType<EntityCreateResult>(await DoorCreateTool.Execute(sc.Manager,
                new DoorCreateRequest([new DoorItem($"MCP_DoorTypeGuard_{Guid.NewGuid():N}")]))),
            ids => DoorDeleteTool.Execute(sc.Manager, new DoorDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorDeleteTool.Execute(sc.Manager,
            new DoorDeleteRequest([created.Guid]));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, [created.Guid]);
    }

    [Fact]
    public async Task Delete_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var create1 = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);

        var create2 = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem($"MCP_Door_{Guid.NewGuid():N}")]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created1.Guid);
        sc.TrackForCleanup(created2.Guid);

        var result = await DoorDeleteTool.Execute(sc.Manager,
            new DoorDeleteRequest([created1.Guid, created2.Guid]));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, [created1.Guid, created2.Guid]);
    }
}
