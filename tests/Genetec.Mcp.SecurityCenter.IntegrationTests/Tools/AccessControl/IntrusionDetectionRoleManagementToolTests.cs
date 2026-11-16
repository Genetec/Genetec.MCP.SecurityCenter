using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class IntrusionDetectionRoleManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    public static IEnumerable<object[]> SastGuardActions => Enum.GetValues<IntrusionDetectionRoleAction>()
        .Where(action => action != IntrusionDetectionRoleAction.List && action != IntrusionDetectionRoleAction.Create).Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(SastGuardActions))]
    public async Task WrongEntityType_RejectsAndPreservesUserGroup(IntrusionDetectionRoleAction action)
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var id = wrong.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(action, Id: id)));
        Assert.Equal("id", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", wrong.Name);
    }


    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Create_Single_ReturnsEntityCreateResult()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_IDRole_{Guid.NewGuid():N}";
        var result = await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create, Items: [name]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
    }

    [Fact]
    public async Task Get_ExistingRole_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_IDRole_{Guid.NewGuid():N}";
        var createResult = await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Create, Items: [name]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);

        var result = await IntrusionDetectionRoleManagementTool.Execute(sc.Manager,
            new IntrusionDetectionRoleRequest(IntrusionDetectionRoleAction.Get, Id: created.Guid));

        Assert.IsType<string>(result);
        Assert.NotEmpty((string)result);
    }

}
