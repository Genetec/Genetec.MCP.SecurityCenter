using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Administration;

public class PartitionManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Theory]
    [InlineData(PartitionAction.Get)]
    [InlineData(PartitionAction.GetMembers)]
    [InlineData(PartitionAction.Update)]
    [InlineData(PartitionAction.AddMember)]
    [InlineData(PartitionAction.RemoveMember)]
    [InlineData(PartitionAction.Delete)]
    public async Task CurrentEntityTargets_RejectWrongEntityTypeAndPreserveEntity(PartitionAction action)
    {
        await IntegrationTestHelpers.AssertWrongEntityTypeAsync(sc, id => PartitionManagementTool.Execute(sc.Manager, (action is PartitionAction.Update or PartitionAction.Delete ? new PartitionManagementRequest(action, Items: [new PartitionManagementItem(Name: action == PartitionAction.Update ? "Rejected name" : null, Id: id)]) : new PartitionManagementRequest(action, Id: id, MemberId: Guid.NewGuid()))), action is PartitionAction.Update or PartitionAction.Delete ? "items" : "id", "UserGroup");
    }

    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetListUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Partition_{Guid.NewGuid():N}";

        var createResult = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Create, Items: [new PartitionManagementItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);

        var getResult = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        var listResult = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.List));
        Assert.NotNull(listResult);

        var updatedName = $"MCP_Partition_Updated_{Guid.NewGuid():N}";
        var updateResult = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Update, Items: [new PartitionManagementItem(Name: updatedName, Id: created.Guid)]));
        Assert.IsType<string>(updateResult);

        var deleteResult = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Delete, Items: [new PartitionManagementItem(Id: created.Guid)]));
        Assert.NotNull(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task Create_BatchMode_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        const int count = 2;
        var names = Enumerable.Range(1, count)
            .Select(i => $"MCP_Partition_Batch_{i}_{Guid.NewGuid():N}")
            .ToList();

        var result = await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Create, Items: names.Select(name => new PartitionManagementItem(Name: name)).ToList()));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(count, batch.RequestedCount);
        Assert.Equal(count, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });
    }

    [Fact]
    public async Task AddAndRemoveMember_UpdatesPartitionMembership()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var partitionName = $"MCP_Partition_Members_{Guid.NewGuid():N}";
        var memberName = $"MCP_Partition_Member_{Guid.NewGuid():N}";

        var partitionResult = Assert.IsType<EntityCreateResult>(await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Create, Items: [new PartitionManagementItem(Name: partitionName)])));
        var memberResult = Assert.IsType<EntityCreateResult>(await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Create, Items: [new PartitionManagementItem(Name: memberName)])));

        // Delete the child first during fixture cleanup so the containing partition can be removed.
        sc.TrackForCleanup(memberResult.Guid);
        sc.TrackForCleanup(partitionResult.Guid);

        var addResult = await PartitionManagementTool.Execute(
            sc.Manager,
            new PartitionManagementRequest(PartitionAction.AddMember, Id: partitionResult.Guid, MemberId: memberResult.Guid));
        Assert.IsType<string>(addResult);

        var afterAdd = Assert.IsType<string>(await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Get, Id: partitionResult.Guid)));
        using (var document = JsonDocument.Parse(afterAdd))
        {
            Assert.Contains(memberResult.Guid.ToString(),
                document.RootElement.GetProperty("Members").EnumerateArray().Select(x => x.GetString()));
        }

        var removeResult = await PartitionManagementTool.Execute(
            sc.Manager,
            new PartitionManagementRequest(PartitionAction.RemoveMember, Id: partitionResult.Guid, MemberId: memberResult.Guid));
        Assert.IsType<string>(removeResult);

        var afterRemove = Assert.IsType<string>(await PartitionManagementTool.Execute(
            sc.Manager, new PartitionManagementRequest(PartitionAction.Get, Id: partitionResult.Guid)));
        using (var document = JsonDocument.Parse(afterRemove))
        {
            Assert.DoesNotContain(memberResult.Guid.ToString(),
                document.RootElement.GetProperty("Members").EnumerateArray().Select(x => x.GetString()));
        }
    }
}
