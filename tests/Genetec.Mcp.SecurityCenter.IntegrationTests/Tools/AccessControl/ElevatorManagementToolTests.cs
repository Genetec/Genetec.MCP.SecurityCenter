using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class ElevatorManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsString()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ElevatorManagementTool.Execute(sc.Manager,
            new ElevatorManagementRequest(ElevatorAction.List));

        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Elevator_{Guid.NewGuid():N}";

        // Create
        var createResult = await ElevatorManagementTool.Execute(sc.Manager,
            new ElevatorManagementRequest(ElevatorAction.Create, Items: [new ElevatorManagementItem(Name: name)]));

        var created = Assert.IsType<EntityCreateResult>(createResult);
        sc.TrackForCleanup(created.Guid);
        var guidStr = created.Guid;

        // Get
        var getResult = await ElevatorManagementTool.Execute(sc.Manager,
            new ElevatorManagementRequest(ElevatorAction.Get, Id: guidStr));

        Assert.IsType<string>(getResult);
        Assert.NotEmpty((string)getResult);

        // Update
        var updateResult = await ElevatorManagementTool.Execute(sc.Manager,
            new ElevatorManagementRequest(ElevatorAction.Update, Items: [new ElevatorManagementItem(Id: guidStr, Body: new ElevatorUpdateProperties(Name: $"{name}_Updated"))]));

        Assert.NotNull(updateResult);

        // Delete
        var deleteResult = await ElevatorManagementTool.Execute(sc.Manager,
            new ElevatorManagementRequest(ElevatorAction.Delete, Items: [new ElevatorManagementItem(Id: guidStr)]));

        Assert.NotNull(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task GetUpdateDelete_UserGroupGuid_RejectsAndPreservesUserGroup()
    {
        var wrongTarget = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var requests = new[]
        {
            new ElevatorManagementRequest(ElevatorAction.Get, Id: wrongTarget.Id),
            new ElevatorManagementRequest(ElevatorAction.Update, Items: [new ElevatorManagementItem(Id: wrongTarget.Id, Body: new ElevatorUpdateProperties(Name: "Must not update"))]),
            new ElevatorManagementRequest(ElevatorAction.Delete, Items: [new ElevatorManagementItem(Id: wrongTarget.Id)])
        };

        foreach (var request in requests)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() =>
                ElevatorManagementTool.Execute(sc.Manager, request));
            Assert.Equal(request.Action == ElevatorAction.Get ? "id" : "items", error.ParamName);
        }

        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongTarget.Id, "UserGroup", wrongTarget.Name);
    }
}
