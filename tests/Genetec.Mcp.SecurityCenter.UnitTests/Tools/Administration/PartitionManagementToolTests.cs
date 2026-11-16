using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class PartitionManagementToolTests
{
    [Theory]
    [InlineData(PartitionAction.Get)]
    [InlineData(PartitionAction.GetMembers)]
    [InlineData(PartitionAction.Update)]
    [InlineData(PartitionAction.AddMember)]
    [InlineData(PartitionAction.RemoveMember)]
    [InlineData(PartitionAction.Delete)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(PartitionAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => PartitionManagementTool.Execute(manager, (action is PartitionAction.Update or PartitionAction.Delete ? new PartitionManagementRequest(action, Items: [new PartitionManagementItem(Name: action == PartitionAction.Update ? "Rejected name" : null, Id: id)]) : new PartitionManagementRequest(action, Id: id, MemberId: Guid.NewGuid()))), action is PartitionAction.Update or PartitionAction.Delete ? "items" : "id", "UserGroup");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PartitionManagementTool.Execute(TestHelpers.FakeManager(),
                new PartitionManagementRequest(PartitionAction.Create, Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PartitionManagementTool.Execute(TestHelpers.FakeManager(),
                new PartitionManagementRequest(PartitionAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PartitionAction.GetMembers, "get_members")]
    [InlineData(PartitionAction.AddMember, "add_member")]
    [InlineData(PartitionAction.RemoveMember, "remove_member")]
    public async Task MemberAction_NullPartitionId_ThrowsArgumentException(
        PartitionAction action, string actionName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PartitionManagementTool.Execute(TestHelpers.FakeManager(),
                (action is PartitionAction.Update or PartitionAction.Delete ? new PartitionManagementRequest(action) : new PartitionManagementRequest(action, MemberId: Guid.NewGuid()))));

        Assert.Contains(action is PartitionAction.Update or PartitionAction.Delete ? "items" : "id", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PartitionAction.AddMember, "add_member")]
    [InlineData(PartitionAction.RemoveMember, "remove_member")]
    public async Task MemberAction_NullMemberId_ThrowsArgumentException(
        PartitionAction action, string actionName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            PartitionManagementTool.Execute(TestHelpers.FakeManager(),
                (action is PartitionAction.Update or PartitionAction.Delete ? new PartitionManagementRequest(action, Items: [new PartitionManagementItem(Id: Guid.NewGuid())]) : new PartitionManagementRequest(action, Id: Guid.NewGuid()))));

        Assert.Contains("member_id", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
