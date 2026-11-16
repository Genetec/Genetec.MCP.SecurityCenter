using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class VisitorGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingVisitor_ReturnsEntityWithName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Visitor_{Guid.NewGuid():N}";

        var createResult = await VisitorCreateTool.Execute(sc.Manager,
            new VisitorCreateRequest([new VisitorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await VisitorGetTool.Execute(sc.Manager, new VisitorGetTool.VisitorGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VisitorGetTool.Execute(sc.Manager,
            new VisitorGetTool.VisitorGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
