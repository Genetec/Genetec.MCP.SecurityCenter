using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserSecurityToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
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
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(UserSecurityAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => UserSecurityTool.Execute(sc.Manager, new UserSecurityRequest(action, id, Password: "Rejected password", PrivilegeId: Guid.NewGuid(), TargetEntityId: Guid.NewGuid(), PrivilegeState: UserPrivilegeState.Granted)), "id", "UserGroup");
    }

    [Theory]
    [InlineData(UserPrivilegeState.Granted)]
    [InlineData(UserPrivilegeState.Denied)]
    [InlineData(UserPrivilegeState.Undefined)]
    public async Task SetPrivilege_CreatedUser_PersistsOnlyRequestedState(UserPrivilegeState state)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var created = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_Privilege_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var privilegeId = Guid.Parse("a3ef1ed7-b755-4774-b23c-b6ef3cb2b9fc");

        // Start from an explicit grant so Undefined must actually clear it.
        await UserSecurityTool.Execute(sc.Manager, new UserSecurityRequest(
            UserSecurityAction.SetPrivilege, created.Guid, PrivilegeId: privilegeId,
            PrivilegeState: UserPrivilegeState.Granted));
        var before = await ReadPrivilegeStates(created.Guid);

        var result = Assert.IsType<string>(await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.SetPrivilege, created.Guid,
                PrivilegeId: privilegeId, PrivilegeState: state)));
        using var response = JsonDocument.Parse(result);
        Assert.Equal("Ok", response.RootElement.GetProperty("status").GetString());
        var after = await ReadPrivilegeStates(created.Guid);
        Assert.Equal(state.ToString(), after[privilegeId]);
        Assert.Equal(before.Where(p => p.Key != privilegeId).OrderBy(p => p.Key),
            after.Where(p => p.Key != privilegeId).OrderBy(p => p.Key));
        using var entity = JsonDocument.Parse(await sc.Manager.GetClient().GetEntity(created.Guid.ToString()));
        Assert.Equal(created.Guid, entity.RootElement.GetProperty("Guid").GetGuid());
    }

    private async Task<Dictionary<Guid, string?>> ReadPrivilegeStates(Guid userId)
    {
        var json = Assert.IsType<string>(await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.GetAccessRights, userId)));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("Privileges").EnumerateArray()
            .ToDictionary(p => p.GetProperty("PrivilegeGuid").GetGuid(), p => p.GetProperty("State").GetString());
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_CreatedUser_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var result = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.GetStatus, Id: createResult.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAccessRights_CreatedUser_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var result = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.GetAccessRights, Id: createResult.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Activate_Deactivate_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var deactivateResult = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.Deactivate, Id: createResult.Guid));
        Assert.NotNull(deactivateResult);

        var activateResult = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.Activate, Id: createResult.Guid));
        Assert.NotNull(activateResult);
    }

    [Fact]
    public async Task IsAdministrator_CreatedUser_ReturnsResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var result = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.IsAdministrator, Id: createResult.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetPassword_CreatedUser_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var result = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.SetPassword, Id: createResult.Guid, Password: "P@ssw0rd!"));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HasAccess_AndHasPrivilege_CreatedUser_ReachesSecurityCenter()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var createResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(createResult.Guid!);

        var access = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.HasAccess,
                Id: createResult.Guid, TargetEntityId: Guid.NewGuid()));
        var privilege = await UserSecurityTool.Execute(sc.Manager,
            new UserSecurityRequest(UserSecurityAction.HasPrivilege,
                Id: createResult.Guid, PrivilegeId: Guid.NewGuid()));

        Assert.NotNull(access);
        Assert.NotNull(privilege);
    }
}
