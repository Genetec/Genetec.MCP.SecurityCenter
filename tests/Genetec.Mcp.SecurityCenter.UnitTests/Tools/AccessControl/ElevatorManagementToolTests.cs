using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class ElevatorManagementToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ElevatorManagementTool.Execute(TestHelpers.FakeManager(),
                new ElevatorManagementRequest(ElevatorAction.Update, Items: [new ElevatorManagementItem(Id: null, Body: new ElevatorUpdateProperties(Name: "test"))])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullBody_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ElevatorManagementTool.Execute(TestHelpers.FakeManager(),
                new ElevatorManagementRequest(ElevatorAction.Update, Items: [new ElevatorManagementItem(Id: Guid.NewGuid(), Body: null)])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ElevatorManagementTool.Execute(TestHelpers.FakeManager(),
                new ElevatorManagementRequest(ElevatorAction.Get)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ElevatorManagementTool.Execute(TestHelpers.FakeManager(),
                new ElevatorManagementRequest(ElevatorAction.Delete)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(MissingCreateItems))]
    public async Task Create_MissingOrEmptyItems_ThrowsArgumentException(List<ElevatorManagementItem>? items)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ElevatorManagementTool.Execute(TestHelpers.FakeManager(),
                new ElevatorManagementRequest(ElevatorAction.Create, Items: items)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object?[]> MissingCreateItems()
    {
        yield return new object?[] { null };
        yield return new object?[] { new List<ElevatorManagementItem>() };
    }

    [Theory]
    [InlineData(ElevatorAction.Get)]
    [InlineData(ElevatorAction.Update)]
    [InlineData(ElevatorAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectBeforeReadOrMutation(ElevatorAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action switch
        {
            ElevatorAction.Get => (action is ElevatorAction.Update or ElevatorAction.Delete ? new ElevatorManagementRequest(action, Items: [new ElevatorManagementItem(Id: id, Body: action == ElevatorAction.Update ? new ElevatorUpdateProperties(Name: "Must not update") : null)]) : new ElevatorManagementRequest(action, Id: id)),
            ElevatorAction.Update => (action is ElevatorAction.Update or ElevatorAction.Delete ? new ElevatorManagementRequest(action, Items: [new ElevatorManagementItem(Id: id, Body: action == ElevatorAction.Update ? new ElevatorUpdateProperties(Name: "Should not update") : null)]) : new ElevatorManagementRequest(action, Id: id)),
            _ => (action is ElevatorAction.Update or ElevatorAction.Delete ? new ElevatorManagementRequest(action, Items: [new ElevatorManagementItem(Id: id, Body: action == ElevatorAction.Update ? new ElevatorUpdateProperties(Name: "Must not update") : null)]) : new ElevatorManagementRequest(action, Id: id))
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => ElevatorManagementTool.Execute(
            TestHelpers.FakeManager(handler), request));

        Assert.Equal(action is ElevatorAction.Update or ElevatorAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, observed => observed.Method != HttpMethod.Get);
    }
}
