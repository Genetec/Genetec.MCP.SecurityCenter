using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserConfigurationToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task WrongUserType_RejectsAndPreservesUserGroup()
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        foreach (var action in Enum.GetValues<UserConfigurationAction>().Where(action => action != UserConfigurationAction.Search))
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => UserConfigurationTool.Execute(sc.Manager,
                new UserConfigurationRequest(action, Id: wrong.Id, UserGroupId: Guid.NewGuid(),
                    PtzEntityId: Guid.NewGuid(), PtzPriority: 50, Domain: "test", ExternalId: "test", TargetEntityId: Guid.NewGuid())));
            Assert.Equal("id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    [Theory]
    [InlineData(UserConfigurationAction.AddToGroup, "user_group_id")]
    [InlineData(UserConfigurationAction.RemoveFromGroup, "user_group_id")]
    [InlineData(UserConfigurationAction.GetSecurityLevelForCamera, "target_entity_id")]
    public async Task WrongLinkedType_RejectsAndPreservesUser(UserConfigurationAction action, string parameter)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_UserLinkedType_{Guid.NewGuid():N}";
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(name)])));
        sc.TrackForCleanup(user.Guid);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(action, Id: user.Guid, UserGroupId: user.Guid, TargetEntityId: user.Guid)));
        Assert.Equal(parameter, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, user.Guid, "User", name);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(GeneralUserStatus.Activated)]
    [InlineData(GeneralUserStatus.Deactivated)]
    public async Task Search_StatusFilters_IncludeAndExcludeTrackedUser(GeneralUserStatus status)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_UserStatusSearch_{Guid.NewGuid():N}";
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(name)])));
        sc.TrackForCleanup(user.Guid);
        var action = status == GeneralUserStatus.Activated ? UserSecurityAction.Activate : UserSecurityAction.Deactivate;
        await UserSecurityTool.Execute(sc.Manager, new UserSecurityRequest(action, user.Guid));
        using var state = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntityFields(user.Guid.ToString(), "Guid", "UserStatus"));
        var entity = IntegrationTestHelpers.GetResult(state.RootElement);
        Assert.Equal(user.Guid, entity.GetProperty("Guid").GetGuid());
        Assert.Equal(status.ToString(), entity.GetProperty("UserStatus").GetString());

        var request = new UserConfigurationRequest(UserConfigurationAction.Search,
            Name: name, NameSearchMode: EntityNameSearchMode.Is, Status: [status]);
        var included = Assert.IsType<string>(await UserConfigurationTool.Execute(sc.Manager, request));
        Assert.Contains(user.Guid, IntegrationTestHelpers.ExtractGuids(included));
        var otherStatus = status == GeneralUserStatus.Activated ? GeneralUserStatus.Deactivated : GeneralUserStatus.Activated;
        var excluded = Assert.IsType<string>(await UserConfigurationTool.Execute(sc.Manager, request with { Status = [otherStatus] }));
        Assert.DoesNotContain(user.Guid, IntegrationTestHelpers.ExtractGuids(excluded));
        var combined = Assert.IsType<string>(await UserConfigurationTool.Execute(sc.Manager,
            request with { Status = [GeneralUserStatus.Activated, GeneralUserStatus.Deactivated] }));
        Assert.Contains(user.Guid, IntegrationTestHelpers.ExtractGuids(combined));
    }

    [Theory]
    [InlineData("%2CGuidsOnly%3DTrue", true)]
    [InlineData("#literal", true)]
    [InlineData(", (literal)", true)]
    [InlineData(@"site\item", false)]
    [InlineData("+literal", false)]
    [InlineData("@literal", false)]
    public async Task Search_LiteralQueryCharacters_ReturnsOnlyMatchingUser(string suffix, bool allowedInUserName)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var name = $"MCP_UserSearch_{Guid.NewGuid():N}" + (allowedInUserName ? suffix : "");
        var user = Assert.IsType<EntityCreateResult>(await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem(name)])));
        sc.TrackForCleanup(user.Guid);
        var request = new UserConfigurationRequest(UserConfigurationAction.Search,
            Name: name, NameSearchMode: EntityNameSearchMode.Is);

        var matching = Assert.IsType<string>(await UserConfigurationTool.Execute(sc.Manager, request));
        Assert.Contains(user.Guid, IntegrationTestHelpers.ExtractGuids(matching));
        var missing = Assert.IsType<string>(await UserConfigurationTool.Execute(sc.Manager,
            request with { Name = name + (allowedInUserName ? "&missing" : suffix) }));
        Assert.DoesNotContain(user.Guid, IntegrationTestHelpers.ExtractGuids(missing));
    }

    [Fact]
    public async Task AddToGroup_RemoveFromGroup_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        // Create user
        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        // Create group
        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        // AddToGroup
        var addResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.AddToGroup,
                Id: userResult.Guid, UserGroupId: groupResult.Guid));
        Assert.IsType<string>(addResult);

        // RemoveFromGroup
        var removeResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.RemoveFromGroup,
                Id: userResult.Guid, UserGroupId: groupResult.Guid));
        Assert.IsType<string>(removeResult);
    }

    [Fact]
    public async Task EnableDisableWatermarking_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        // Break inheritance so watermarking can be set directly on the user
        await sc.Manager.GetClient().UpdateEntity(userResult.Guid.ToString(),
            new { IsUserWatermarkingConfigurationInherited = false });

        var enableResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.EnableWatermarking, Id: userResult.Guid));
        Assert.IsType<string>(enableResult);

        var disableResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.DisableWatermarking, Id: userResult.Guid));
        Assert.IsType<string>(disableResult);
    }

    [Fact]
    public async Task AddRemovePtzPriority_ExistingUserAndCamera_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var cameraJson = await sc.Manager.GetClient().ListEntities("Camera", 1, 1);
        var cameraId = ScIntegrationFixture.ExtractFirstGuid(cameraJson);
        Assert.NotNull(cameraId);

        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        var addResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.AddPtzPriority,
                Id: userResult.Guid, PtzEntityId: cameraId, PtzPriority: 50));
        Assert.IsType<string>(addResult);
        var afterAdd = await sc.Manager.GetClient().GetEntityField(userResult.Guid.ToString(), "PtzPriorities");
        Assert.Contains(cameraId.Value.ToString(), afterAdd, StringComparison.OrdinalIgnoreCase);

        var removeResult = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.RemovePtzPriority,
                Id: userResult.Guid, PtzEntityId: cameraId, PtzPriority: 50));
        Assert.IsType<string>(removeResult);
        var afterRemove = await sc.Manager.GetClient().GetEntityField(userResult.Guid.ToString(), "PtzPriorities");
        Assert.DoesNotContain(cameraId.Value.ToString(), afterRemove, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSecurityLevelForCamera_ExistingUserAndCamera_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var cameraJson = await sc.Manager.GetClient().ListEntities("Camera", 1, 1);
        var cameraId = ScIntegrationFixture.ExtractFirstGuid(cameraJson);
        Assert.NotNull(cameraId);

        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        var result = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.GetSecurityLevelForCamera,
                Id: userResult.Guid, TargetEntityId: cameraId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetDomain_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        const string domain = "mcp, (literal) @corp.example.local";
        var result = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.SetDomain,
                Id: userResult.Guid, Domain: domain));
        Assert.IsType<string>(result);

        using var readback = JsonDocument.Parse(await sc.Manager.GetClient()
            .GetEntityFields(userResult.Guid.ToString(), "Domain"));
        Assert.Equal(domain, readback.RootElement.GetProperty("Domain").GetString());
    }

    [Fact]
    public async Task SetExternalId_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var userResult = (EntityCreateResult)await UserCreateTool.Execute(sc.Manager,
            new UserCreateRequest([new UserItem($"MCP_User_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(userResult.Guid);

        const string externalId = @"EXT, (literal) @site&east+%50\item";
        var result = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.SetExternalId,
                Id: userResult.Guid, ExternalId: externalId));
        Assert.IsType<string>(result);

        using var readback = JsonDocument.Parse(await sc.Manager.GetClient()
            .GetEntityFields(userResult.Guid.ToString(), "ExternalUniqueId"));
        Assert.Equal(externalId, readback.RootElement.GetProperty("ExternalUniqueId").GetString());
    }

    [Fact]
    public async Task Search_NoFilter_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.Search));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Search_ByName_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var result = await UserConfigurationTool.Execute(sc.Manager,
            new UserConfigurationRequest(UserConfigurationAction.Search,
                Name: "admin", NameSearchMode: EntityNameSearchMode.Contains));

        Assert.NotNull(result);
    }
}
