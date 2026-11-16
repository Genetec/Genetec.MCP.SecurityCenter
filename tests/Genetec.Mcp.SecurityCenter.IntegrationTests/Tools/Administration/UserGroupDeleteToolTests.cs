using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesOtherEntity(bool invalidFirst)
    {
        await IntegrationTestHelpers.AssertDeleteTypeGuardAsync(sc,
            async () => Assert.IsType<EntityCreateResult>(await UserGroupCreateTool.Execute(sc.Manager,
                new UserGroupCreateRequest([new UserGroupItem($"MCP_UserGroupTypeGuard_{Guid.NewGuid():N}")]))),
            ids => UserGroupDeleteTool.Execute(sc.Manager, new UserGroupDeleteRequest(ids)),
            invalidFirst, wrongType: "Partition");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_CreatedUserGroup_ReturnsStatusOk()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var created = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UGDel_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(created.Guid);

        var result = await UserGroupDeleteTool.Execute(sc.Manager, new UserGroupDeleteRequest([created.Guid]));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, [created.Guid]);
    }

    [Fact]
    public async Task Delete_BatchMode_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var guids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var created = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
                new UserGroupCreateRequest([new UserGroupItem($"MCP_UGDel_{i}_{Guid.NewGuid():N}")]));
            guids.Add(created.Guid);
            sc.TrackForCleanup(created.Guid);
        }

        var result = await UserGroupDeleteTool.Execute(sc.Manager, new UserGroupDeleteRequest(guids));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, guids);
    }
}
