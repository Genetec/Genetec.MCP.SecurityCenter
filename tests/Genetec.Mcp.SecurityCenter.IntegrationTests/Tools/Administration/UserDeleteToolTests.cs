using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesOtherEntity(bool invalidFirst)
    {
        await IntegrationTestHelpers.AssertDeleteTypeGuardAsync(sc,
            async () => Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
                new UserCreateRequest([new UserItem($"MCP_UserTypeGuard_{Guid.NewGuid():N}")]))),
            ids => UserDeleteTool.Execute(sc.Manager, new UserDeleteRequest(ids)),
            invalidFirst);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_CreatedUsers_VerifiesStatusAndDeletion(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var created = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
                new UserCreateRequest([new UserItem($"mcp_del_{Guid.NewGuid():N}")])));
            sc.TrackForCleanup(created.Guid);
            ids.Add(created.Guid);
        }

        var result = await UserDeleteTool.Execute(sc.Manager, new UserDeleteRequest(ids));
        await IntegrationTestHelpers.AssertDeletedEntitiesAsync(sc, result, ids);
    }
}
