using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class IntrusionDetectionRoleManagementToolTests
{
    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<IntrusionDetectionRoleAction>()
        .Where(action => action != IntrusionDetectionRoleAction.List && action != IntrusionDetectionRoleAction.Create).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsBeforeMutation(IntrusionDetectionRoleAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionDetectionRoleManagementTool.Execute(TestHelpers.FakeManager(handler),
            new IntrusionDetectionRoleRequest(action, Id: id)));
        Assert.Equal("id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }


    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionDetectionRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Get, Id: null)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionDetectionRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create, Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionDetectionRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_SingleItemInvalidAfterSanitization_ThrowsArgumentException(string name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            IntrusionDetectionRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create,
                    Items: [name])));

        Assert.Contains("items[0]", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
