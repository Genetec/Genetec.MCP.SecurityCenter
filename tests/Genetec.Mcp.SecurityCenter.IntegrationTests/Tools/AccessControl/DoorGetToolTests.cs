using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class DoorGetToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_CreatedDoor_ReturnsEntityWithName()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Door_{Guid.NewGuid():N}";

        var createResult = await DoorCreateTool.Execute(sc.Manager,
            new DoorCreateRequest([new DoorItem(name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await DoorGetTool.Execute(sc.Manager, new DoorGetTool.DoorGetRequest(created.Guid));

        Assert.NotNull(result);
        Assert.Contains(name, (string)result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WrongEntityType_RejectsAndPreservesEntity()
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => DoorGetTool.Execute(sc.Manager,
            new DoorGetTool.DoorGetRequest(id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }
}
