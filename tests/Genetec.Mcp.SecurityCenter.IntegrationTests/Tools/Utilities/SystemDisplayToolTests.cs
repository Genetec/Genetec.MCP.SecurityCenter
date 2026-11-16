using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class SystemDisplayToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(DisplayAction.DisplayEntity)]
    [InlineData(DisplayAction.SendUserTaskToMonitor)]
    public async Task WrongTypedTarget_IsRejectedWithoutChangingEntity(DisplayAction action)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var id = Guid.Parse("00000000-0000-0000-0000-000000000007");
        var before = await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemDisplayTool.Execute(sc.Manager,
            new SystemDisplayRequest(action, EntityId: id, DestinationId: id, MonitorId: 1)));
        Assert.Equal(action == DisplayAction.DisplayEntity ? "destination_id" : "entity_id", error.ParamName);
        Assert.Equal(before, await sc.Manager.GetClient().GetEntityFields(id.ToString(), "Guid", "Name"));
    }

    [Fact]
    public async Task DisplayEntity_ReachSecurityCenter()
        => await AssertReachesSecurityCenter(new SystemDisplayRequest(DisplayAction.DisplayEntity,
            EntityId: Guid.NewGuid(), DestinationId: Guid.NewGuid()));

    [Fact]
    public async Task ClearTile_ReachSecurityCenter()
        => await AssertReachesSecurityCenter(new SystemDisplayRequest(DisplayAction.ClearTile,
            MonitorId: 1, TileId: 1));

    [Fact]
    public async Task GetTile_ReachSecurityCenter()
        => await AssertReachesSecurityCenter(new SystemDisplayRequest(DisplayAction.GetTile,
            MonitorId: 1, TileId: 1));

    [Fact]
    public async Task SendUserTaskToMonitor_ReachSecurityCenter()
        => await AssertReachesSecurityCenter(new SystemDisplayRequest(DisplayAction.SendUserTaskToMonitor,
            MonitorId: 1, EntityId: Guid.NewGuid()));

    private async Task AssertReachesSecurityCenter(SystemDisplayRequest request)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var exception = await Record.ExceptionAsync(() => SystemDisplayTool.Execute(sc.Manager, request));
        Assert.True(exception is null or not ArgumentException, exception?.ToString());
    }
}
