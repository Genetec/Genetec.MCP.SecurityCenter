using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class UserGroupSecurityToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(UserGroupSecurityAction.GetAccessRights)]
    [InlineData(UserGroupSecurityAction.HasAccess)]
    [InlineData(UserGroupSecurityAction.HasPrivilege)]
    [InlineData(UserGroupSecurityAction.EnableWatermarking)]
    [InlineData(UserGroupSecurityAction.DisableWatermarking)]
    [InlineData(UserGroupSecurityAction.AddPtzPriority)]
    [InlineData(UserGroupSecurityAction.RemovePtzPriority)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(UserGroupSecurityAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => UserGroupSecurityTool.Execute(sc.Manager, new UserGroupSecurityRequest(action, id, PrivilegeId: Guid.NewGuid(), TargetEntityId: Guid.NewGuid(), PtzEntityId: Guid.NewGuid(), PtzPriority: 1)), "id", "Partition");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessRights_ExistingGroup_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        var result = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.GetAccessRights, Id: groupResult.Guid));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task EnableDisableWatermarking_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        // Break inheritance so watermarking can be set directly on the group
        await sc.Manager.GetClient().UpdateEntity(groupResult.Guid.ToString(),
            new { IsUserWatermarkingConfigurationInherited = false });

        var enableResult = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.EnableWatermarking, Id: groupResult.Guid));
        Assert.NotNull(enableResult);

        var disableResult = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.DisableWatermarking, Id: groupResult.Guid));
        Assert.NotNull(disableResult);
    }

    [Fact]
    public async Task AddRemovePtzPriority_ExistingGroupAndCamera_Succeeds()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var cameraJson = await sc.Manager.GetClient().ListEntities("Camera", 1, 1);
        var cameraId = ScIntegrationFixture.ExtractFirstGuid(cameraJson);
        Assert.NotNull(cameraId);

        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        var addResult = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.AddPtzPriority,
                Id: groupResult.Guid, PtzEntityId: cameraId, PtzPriority: 50));
        Assert.NotNull(addResult);
        var afterAdd = await sc.Manager.GetClient().GetEntityField(groupResult.Guid.ToString(), "PtzPriorities");
        Assert.Contains(cameraId.Value.ToString(), afterAdd, StringComparison.OrdinalIgnoreCase);

        var removeResult = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.RemovePtzPriority,
                Id: groupResult.Guid, PtzEntityId: cameraId, PtzPriority: 50));
        Assert.NotNull(removeResult);
        var afterRemove = await sc.Manager.GetClient().GetEntityField(groupResult.Guid.ToString(), "PtzPriorities");
        Assert.DoesNotContain(cameraId.Value.ToString(), afterRemove, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HasAccess_ExistingGroupAndCamera_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var cameraJson = await sc.Manager.GetClient().ListEntities("Camera", 1, 1);
        var cameraId = ScIntegrationFixture.ExtractFirstGuid(cameraJson);
        Assert.NotNull(cameraId);

        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        var result = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.HasAccess,
                Id: groupResult.Guid, TargetEntityId: cameraId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HasPrivilege_ExistingGroup_ReturnsResult()
    {
        Assert.True(sc.IsAvailable, "Set SC_HOST to run integration tests.");

        var groupResult = (EntityCreateResult)await UserGroupCreateTool.Execute(sc.Manager,
            new UserGroupCreateRequest([new UserGroupItem($"MCP_UG_{Guid.NewGuid():N}")]));
        sc.TrackForCleanup(groupResult.Guid);

        var result = await UserGroupSecurityTool.Execute(sc.Manager,
            new UserGroupSecurityRequest(UserGroupSecurityAction.HasPrivilege,
                Id: groupResult.Guid, PrivilegeId: Guid.NewGuid()));

        Assert.NotNull(result);
    }
}
