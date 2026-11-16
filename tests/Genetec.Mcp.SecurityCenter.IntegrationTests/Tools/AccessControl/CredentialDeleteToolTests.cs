using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CredentialDeleteToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Single_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "9999")]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await CredentialDeleteTool.Execute(sc.Manager,
            new CredentialDeleteRequest([created.Guid]));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_Batch_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var create1 = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "1111")]));
        var created1 = Assert.IsType<EntityCreateResult>(create1);

        var create2 = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "2222")]));
        var created2 = Assert.IsType<EntityCreateResult>(create2);
        sc.TrackForCleanup(created1.Guid);
        sc.TrackForCleanup(created2.Guid);

        var result = await CredentialDeleteTool.Execute(sc.Manager,
            new CredentialDeleteRequest([created1.Guid, created2.Guid]));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_WrongTypeSingleAndMixedBatch_PreservesUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);

        var singleError = await Assert.ThrowsAsync<ArgumentException>(() => CredentialDeleteTool.Execute(sc.Manager,
            new CredentialDeleteRequest([wrongTarget.Id])));
        Assert.Equal("items", singleError.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);

        var created = Assert.IsType<EntityCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin,
                Pin: Random.Shared.Next(1000, 9999).ToString())])));
        sc.TrackForCleanup(created.Guid);

        var batch = await CredentialDeleteTool.Execute(sc.Manager, new CredentialDeleteRequest([created.Guid, wrongTarget.Id]));
        IntegrationTestHelpers.AssertPartialBatchResult(batch, created.Guid, wrongTarget.Id);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
