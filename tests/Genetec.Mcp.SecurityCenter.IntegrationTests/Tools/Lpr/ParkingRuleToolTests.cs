using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Lpr;

public class ParkingRuleToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Crud_CreateGetUpdateDelete_Succeeds()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_ParkingRule_{Guid.NewGuid():N}";

        var createResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Create, Items: [new ParkingRuleBatchItem(Name: name)]));
        var created = Assert.IsType<EntityCreateResult>(createResult);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "ParkingRule", name);

        var getResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Get, Id: created.Guid));
        Assert.IsType<string>(getResult);
        Assert.Contains(name, (string)getResult);

        var updateResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Update, Items: [new ParkingRuleBatchItem(Name: $"{name}_Updated", Id: created.Guid)]));
        Assert.IsType<string>(updateResult);

        await IntegrationTestHelpers.AssertEntityAsync(sc, created.Guid, "ParkingRule", $"{name}_Updated");

        var deleteResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Delete, Items: [new ParkingRuleBatchItem(Id: created.Guid)]));
        Assert.IsType<StatusResult>(deleteResult);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
    }

    [Fact]
    public async Task List_ReturnsJson()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.List));

        Assert.IsType<string>(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TypedItems_CreateReadUpdateDeleteReadback_Succeeds(int count)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var names = Enumerable.Range(0, count).Select(_ => $"MCP_ParkingRule_Typed_{Guid.NewGuid():N}").ToList();
        var createResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Create, Items: names.Select(name => new ParkingRuleBatchItem(Name: name)).ToList()));
        List<Guid> ids;

        if (count == 1)
        {
            var created = Assert.IsType<EntityCreateResult>(createResult);
            sc.TrackForCleanup(created.Guid);
            ids = [created.Guid];
        }
        else
        {
            var batch = Assert.IsType<BatchCreateResult>(createResult);
            foreach (var result in batch.Results)
                if (result.Guid is { } id) sc.TrackForCleanup(id);
            Assert.Equal(count, batch.RequestedCount);
            Assert.Equal(count, batch.SuccessCount);
            Assert.Equal(0, batch.FailureCount);
            ids = batch.Results.Select(result => Assert.IsType<Guid>(result.Guid)).ToList();
        }
        for (var index = 0; index < ids.Count; index++)
        {
            var result = await ParkingRuleTool.Execute(sc.Manager, new ParkingRuleManagementRequest(ParkingRuleAction.Get, Id: ids[index]));
            Assert.IsType<string>(result);
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "ParkingRule", names[index]);
        }
        var updateResult = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Update, Items: ids.Select((id, index) =>
                new ParkingRuleBatchItem(Id: id, Body: new ParkingRuleUpdateProperties(Name: names[index] + "_Updated"))).ToList()));
        if (count > 1)
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(updateResult));
            var batch = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(count, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(0, batch.GetProperty("failure_count").GetInt32());
        }
        for (var index = 0; index < ids.Count; index++)
            await IntegrationTestHelpers.AssertEntityAsync(sc, ids[index], "ParkingRule", names[index] + "_Updated");
        var deleted = await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Delete, Items: ids.Select(id => new ParkingRuleBatchItem(Id: id)).ToList()));
        if (count == 1) Assert.IsType<StatusResult>(deleted);
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(deleted));
            var batch = IntegrationTestHelpers.GetResult(document.RootElement);
            Assert.Equal(count, batch.GetProperty("success_count").GetInt32());
            Assert.Equal(0, batch.GetProperty("failure_count").GetInt32());
        }
        foreach (var id in ids) await IntegrationTestHelpers.AssertEntityMissingAsync(sc, id);
    }

    [Theory]
    [InlineData(ParkingRuleAction.Get)]
    [InlineData(ParkingRuleAction.Update)]
    [InlineData(ParkingRuleAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(ParkingRuleAction action)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = (action == ParkingRuleAction.Get ? new ParkingRuleManagementRequest(action, Id: id) : new ParkingRuleManagementRequest(action, Items: [new ParkingRuleBatchItem(Id: id, Body: action == ParkingRuleAction.Update ? new ParkingRuleUpdateProperties(Name: "Must not update") : null)]));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingRuleTool.Execute(sc.Manager, request));
        Assert.Equal(action == ParkingRuleAction.Get ? "id" : "items", error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData("Permit", "items.body.permit")]
    public async Task Update_WrongLinkedEntityType_RejectsBeforeMutation(string property, string parameter)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_LinkType_{Guid.NewGuid():N}";
        var created = Assert.IsType<Genetec.Mcp.SecurityCenter.Client.EntityCreateResult>(await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Create, Items: [new ParkingRuleBatchItem(Name: name)])));
        var id = created.Guid;
        sc.TrackForCleanup(id);
        var body = property switch
        {
            "Permit" => new ParkingRuleUpdateProperties(Permit: wrongId),

            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Update, Items: [new ParkingRuleBatchItem(Id: id, Body: body)])));
        Assert.Equal(parameter, error.ParamName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "ParkingRule", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_WrongEntityTypeInItems_RejectsSingleAndBatch(int count)
    {
        var (id, name) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var request = new ParkingRuleManagementRequest(ParkingRuleAction.Delete, Items: Enumerable.Range(0, count).Select(_ => new ParkingRuleBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingRuleTool.Execute(sc.Manager, request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = IntegrationTestHelpers.ParseJson(Assert.IsType<string>(await ParkingRuleTool.Execute(sc.Manager, request)));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, id, "UserGroup", name);
    }

    [Theory]
    [InlineData("Permit")]
    public async Task BatchUpdate_WrongLinkedType_PreservesValidUpdate(string property)
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var names = new[] { $"MCP_ParkingRule_Valid_{Guid.NewGuid():N}", $"MCP_ParkingRule_Rejected_{Guid.NewGuid():N}" };
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var created = Assert.IsType<EntityCreateResult>(await ParkingRuleTool.Execute(sc.Manager,
                new ParkingRuleManagementRequest(ParkingRuleAction.Create, Items: [new ParkingRuleBatchItem(Name: name)])));
            sc.TrackForCleanup(created.Guid);
            ids.Add(created.Guid);
        }
        var invalidBody = property switch
        {
            "Permit" => new ParkingRuleUpdateProperties(Permit: wrongId),
            _ => throw new InvalidOperationException()
        };
        var result = await ParkingRuleTool.Execute(sc.Manager, new ParkingRuleManagementRequest(ParkingRuleAction.Update, Items:
        [
            new ParkingRuleBatchItem(Id: ids[0], Body: new ParkingRuleUpdateProperties(Name: names[0] + "_Updated")),
            new ParkingRuleBatchItem(Id: ids[1], Body: invalidBody)
        ]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, ids[0], ids[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[0], "ParkingRule", names[0] + "_Updated");
        await IntegrationTestHelpers.AssertEntityAsync(sc, ids[1], "ParkingRule", names[1]);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }

    [Fact]
    public async Task BatchDelete_WrongType_PreservesValidDelete()
    {
        var (wrongId, wrongName) = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var created = Assert.IsType<EntityCreateResult>(await ParkingRuleTool.Execute(sc.Manager,
            new ParkingRuleManagementRequest(ParkingRuleAction.Create, Items: [new ParkingRuleBatchItem(Name: $"MCP_ParkingRule_Delete_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(created.Guid);
        var result = await ParkingRuleTool.Execute(sc.Manager, new ParkingRuleManagementRequest(ParkingRuleAction.Delete, Items:
            [new ParkingRuleBatchItem(Id: created.Guid), new ParkingRuleBatchItem(Id: wrongId)]));
        IntegrationTestHelpers.AssertPartialBatchResult(result, created.Guid, wrongId);
        await IntegrationTestHelpers.AssertEntityMissingAsync(sc, created.Guid);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrongId, "UserGroup", wrongName);
    }
}
