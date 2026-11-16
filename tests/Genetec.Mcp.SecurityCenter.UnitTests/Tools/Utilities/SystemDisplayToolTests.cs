using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class SystemDisplayToolTests
{
    [Theory]
    [InlineData(DisplayAction.DisplayEntity)]
    [InlineData(DisplayAction.SendUserTaskToMonitor)]
    public async Task EmptyTypedTarget_IsRejected(DisplayAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => SystemDisplayTool.Execute(
            TestHelpers.FakeManager(), new SystemDisplayRequest(action,
                EntityId: Guid.Empty, DestinationId: Guid.Empty, MonitorId: 1)));
        Assert.Equal(action == DisplayAction.DisplayEntity ? "destination_id" : "entity_id", error.ParamName);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task DisplayEntity_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.DisplayEntity,
                    DestinationId: Guid.NewGuid())));
        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisplayEntity_MissingDestinationId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.DisplayEntity,
                    EntityId: Guid.NewGuid())));
        Assert.Contains("destination_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearTile_MissingMonitorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.ClearTile, TileId: 1)));
        Assert.Contains("monitor_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearTile_MissingTileId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.ClearTile, MonitorId: 1)));
        Assert.Contains("tile_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTile_MissingMonitorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.GetTile, TileId: 1)));
        Assert.Contains("monitor_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendUserTaskToMonitor_MissingMonitorId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.SendUserTaskToMonitor,
                    EntityId: Guid.NewGuid())));
        Assert.Contains("monitor_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendUserTaskToMonitor_MissingEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SystemDisplayTool.Execute(TestHelpers.FakeManager(),
                new SystemDisplayRequest(DisplayAction.SendUserTaskToMonitor,
                    MonitorId: 5)));
        Assert.Contains("entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
