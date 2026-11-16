using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserGroupSecurityToolTests
{
    [Theory]
    [InlineData(UserGroupSecurityAction.GetAccessRights)]
    [InlineData(UserGroupSecurityAction.HasAccess)]
    [InlineData(UserGroupSecurityAction.HasPrivilege)]
    [InlineData(UserGroupSecurityAction.EnableWatermarking)]
    [InlineData(UserGroupSecurityAction.DisableWatermarking)]
    [InlineData(UserGroupSecurityAction.AddPtzPriority)]
    [InlineData(UserGroupSecurityAction.RemovePtzPriority)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(UserGroupSecurityAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => UserGroupSecurityTool.Execute(manager, new UserGroupSecurityRequest(action, id, PrivilegeId: Guid.NewGuid(), TargetEntityId: Guid.NewGuid(), PtzEntityId: Guid.NewGuid(), PtzPriority: 1)), "id", "Partition");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task HasAccess_NullTargetEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.HasAccess, Id: Guid.NewGuid(), TargetEntityId: null)));

        Assert.Contains("target_entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HasPrivilege_NullPrivilegeId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.HasPrivilege, Id: Guid.NewGuid(), PrivilegeId: null)));

        Assert.Contains("privilege_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddPtzPriority_NullPtzEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.AddPtzPriority, Id: Guid.NewGuid(),
                    PtzEntityId: null, PtzPriority: 5)));

        Assert.Contains("ptz_entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddPtzPriority_NullPtzPriority_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.AddPtzPriority, Id: Guid.NewGuid(),
                    PtzEntityId: Guid.NewGuid(), PtzPriority: null)));

        Assert.Contains("ptz_priority", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UserGroupSecurityAction.AddPtzPriority)]
    [InlineData(UserGroupSecurityAction.RemovePtzPriority)]
    public async Task PtzPriorityActions_UseWebSdkSignature(UserGroupSecurityAction action)
    {
        var handler = new ToolTestHandler();
        var metadata = new Tools.EntityMetadataTestHandler(handler);
        var manager = TestHelpers.FakeManager(metadata);
        var groupId = Guid.NewGuid();
        metadata.AddEntity(groupId, "UserGroup");
        var entityId = Guid.NewGuid();

        var result = await UserGroupSecurityTool.Execute(manager,
            new UserGroupSecurityRequest(
                action,
                Id: groupId,
                PtzEntityId: entityId,
                PtzPriority: 50));

        Assert.NotNull(result);
        var request = Assert.Single(handler.Requests);
        var expectedMethod = action == UserGroupSecurityAction.AddPtzPriority
            ? "AddPtzPriority"
            : "RemovePtzPriority";
        Assert.Equal(
            $"?q=entity={groupId},{expectedMethod}(PtzPriority({entityId},50))",
            Uri.UnescapeDataString(request.Query));
        Assert.DoesNotContain("\\,", Uri.UnescapeDataString(request.Query));
        Assert.Contains("%3D", request.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemovePtzPriority_NullPtzEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.RemovePtzPriority, Id: Guid.NewGuid(),
                    PtzEntityId: null, PtzPriority: 5)));

        Assert.Contains("ptz_entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemovePtzPriority_NullPtzPriority_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserGroupSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserGroupSecurityRequest(UserGroupSecurityAction.RemovePtzPriority, Id: Guid.NewGuid(),
                    PtzEntityId: Guid.NewGuid(), PtzPriority: null)));

        Assert.Contains("ptz_priority", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
