using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class AlarmDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesOtherEntity(bool invalidFirst)
    {
        await IntegrationTestHelpers.AssertDeleteTypeGuardAsync(sc,
            async () => Assert.IsType<EntityCreateResult>(await AlarmCreateTool.Execute(sc.Manager,
                new AlarmCreateRequest([new AlarmItem($"MCP_AlarmTypeGuard_{Guid.NewGuid():N}")]))),
            ids => AlarmDeleteTool.Execute(sc.Manager, new AlarmDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_CreatedAlarms_VerifiesStatusAndDeletion(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var created = Assert.IsType<EntityCreateResult>(await AlarmCreateTool.Execute(sc.Manager,
                new AlarmCreateRequest([new AlarmItem($"MCP_AlarmDel_{Guid.NewGuid():N}")])));
            sc.TrackForCleanup(created.Guid);
            ids.Add(created.Guid);
        }

        var result = await AlarmDeleteTool.Execute(sc.Manager, new AlarmDeleteRequest(ids));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, ids);
    }
}
