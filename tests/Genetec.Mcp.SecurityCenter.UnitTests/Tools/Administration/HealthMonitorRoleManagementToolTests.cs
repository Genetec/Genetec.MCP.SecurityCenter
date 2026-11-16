using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class HealthMonitorRoleManagementToolTests
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => HealthMonitorRoleManagementTool.Execute(manager, new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.Get, Id: id)), "id");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            HealthMonitorRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new HealthMonitorRoleManagementRequest(HealthMonitorRoleAction.Get)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
