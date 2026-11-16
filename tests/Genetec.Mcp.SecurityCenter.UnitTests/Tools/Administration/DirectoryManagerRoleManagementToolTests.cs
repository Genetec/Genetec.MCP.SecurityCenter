using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class DirectoryManagerRoleManagementToolTests
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => DirectoryManagerRoleManagementTool.Execute(manager, new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get, Id: id)), "id");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DirectoryManagerRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new DirectoryManagerRoleManagementRequest(DirectoryManagerRoleAction.Get)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
