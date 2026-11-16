using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class AuthenticationServiceRoleManagementToolTests
{
    [Fact]
    public async Task CurrentEntityTargets_RejectWrongEntityType()
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => AuthenticationServiceRoleManagementTool.Execute(manager, new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Get, Id: id)), "id");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AuthenticationServiceRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Get)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidItemName_ThrowsArgumentException(string? name)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AuthenticationServiceRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Create, Items: [name!])));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            AuthenticationServiceRoleManagementTool.Execute(TestHelpers.FakeManager(),
                new AuthenticationServiceRoleManagementRequest(AuthenticationServiceRoleAction.Create)));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
