using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class UserSecurityToolTests
{
    [Theory]
    [InlineData(UserSecurityAction.SetPassword)]
    [InlineData(UserSecurityAction.GetAccessRights)]
    [InlineData(UserSecurityAction.Activate)]
    [InlineData(UserSecurityAction.Deactivate)]
    [InlineData(UserSecurityAction.GetStatus)]
    [InlineData(UserSecurityAction.IsAdministrator)]
    [InlineData(UserSecurityAction.HasAccess)]
    [InlineData(UserSecurityAction.HasPrivilege)]
    [InlineData(UserSecurityAction.SetPrivilege)]
    public async Task CurrentEntityTargets_RejectWrongEntityType(UserSecurityAction action)
    {
        await TestHelpers.AssertWrongEntityTypeAsync((manager, id) => UserSecurityTool.Execute(manager, new UserSecurityRequest(action, id, Password: "Rejected password", PrivilegeId: Guid.NewGuid(), TargetEntityId: Guid.NewGuid(), PrivilegeState: UserPrivilegeState.Granted)), "id", "UserGroup");
    }

    [Theory]
    [InlineData("granted", UserPrivilegeState.Granted)]
    [InlineData("denied", UserPrivilegeState.Denied)]
    [InlineData("undefined", UserPrivilegeState.Undefined)]
    public void SetPrivilege_ProductionRequest_BindsTypedInputs(string state, UserPrivilegeState expected)
    {
        var userId = Guid.NewGuid();
        var privilegeId = Guid.NewGuid();
        var json = $$"""{"action":"set_privilege","id":"{{userId}}","privilege_id":"{{privilegeId}}","privilege_state":"{{state}}"}""";
        var request = JsonSerializer.Deserialize<UserSecurityRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Equal(UserSecurityAction.SetPrivilege, request.Action);
        Assert.Equal(userId, request.Id);
        Assert.Equal(privilegeId, request.PrivilegeId);
        Assert.Equal(expected, request.PrivilegeState);
    }

    [Theory]
    [InlineData(true, "privilege_id")]
    [InlineData(false, "privilege_state")]
    public async Task SetPrivilege_MissingConditionalInput_Throws(bool missingId, string parameter)
    {
        var request = new UserSecurityRequest(UserSecurityAction.SetPrivilege, Guid.NewGuid(),
            PrivilegeId: missingId ? null : Guid.NewGuid(),
            PrivilegeState: missingId ? UserPrivilegeState.Granted : null);
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserSecurityTool.Execute(TestHelpers.FakeManager(), request));
        Assert.Contains(parameter, error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task SetPrivilege_InvalidState_Throws(int state)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserSecurityRequest(UserSecurityAction.SetPrivilege, Guid.NewGuid(),
                    PrivilegeId: Guid.NewGuid(), PrivilegeState: (UserPrivilegeState)state)));
        Assert.Contains("privilege_state", error.Message);
    }

    [Fact]
    public void SetPrivilege_UnknownStateText_RejectsBinding()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserSecurityRequest>(
            """{"action":"set_privilege","privilege_state":"none"}""", NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void RequestSchema_ExposesPrivilegeActionAndStates()
    {
        TestHelpers.AssertRequiredProperty<UserSecurityRequest>(nameof(UserSecurityRequest.Action));
        TestHelpers.AssertRequiredProperty<UserSecurityRequest>(nameof(UserSecurityRequest.Id));
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "user_security", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray());
        var parameter = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var fields = parameter.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(fields.Single(f => f.GetProperty("name").GetString() == "action")
            .GetProperty("enum_values").EnumerateArray(), v => v.GetString() == "set_privilege");
        var state = fields.Single(f => f.GetProperty("name").GetString() == "privilege_state");
        Assert.Equal(["denied", "granted", "undefined"],
            state.GetProperty("enum_values").EnumerateArray().Select(v => v.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("description").GetString()));
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task SetPassword_NullPassword_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserSecurityRequest(UserSecurityAction.SetPassword, Id: Guid.NewGuid(), Password: null)));

        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HasAccess_NullTargetEntityId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserSecurityRequest(UserSecurityAction.HasAccess, Id: Guid.NewGuid(), TargetEntityId: null)));

        Assert.Contains("target_entity_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HasPrivilege_NullPrivilegeId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            UserSecurityTool.Execute(TestHelpers.FakeManager(),
                new UserSecurityRequest(UserSecurityAction.HasPrivilege, Id: Guid.NewGuid(), PrivilegeId: null)));

        Assert.Contains("privilege_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
