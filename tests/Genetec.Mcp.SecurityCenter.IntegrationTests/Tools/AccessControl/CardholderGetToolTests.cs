using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CardholderGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingCardholder_ReturnsJsonContainingName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cardholder_{Guid.NewGuid():N}";

        var createResult = await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var result = await CardholderGetTool.Execute(sc.Manager, new CardholderGetTool.CardholderGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CardholderGetTool.Execute(sc.Manager,
            new CardholderGetTool.CardholderGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
