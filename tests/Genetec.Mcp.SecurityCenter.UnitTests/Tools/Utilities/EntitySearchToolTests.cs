using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using Genetec.Mcp.SecurityCenter.UnitTests.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class EntitySearchToolTests
{
    [Fact]
    public async Task AllReadActions_DispatchToSecurityCenter()
    {
        var handler = new ToolTestHandler();
        var manager = TestHelpers.FakeManager(handler);
        var id = Guid.NewGuid();

        async Task Run(EntitySearchRequest request)
            => Assert.NotNull(await EntitySearchTool.Execute(manager, request));

        await Run(new EntitySearchRequest(EntitySearchAction.SearchByType,
            EntityType: [ScEntityType.Camera]));
        await Run(new EntitySearchRequest(EntitySearchAction.SearchByName,
            Name: "Test", EntityType: [ScEntityType.Camera], NameSearchMode: EntityNameSearchMode.Contains));
        await Run(new EntitySearchRequest(EntitySearchAction.SearchByLogicalId,
            EntityType: [ScEntityType.Camera], LogicalId: "1"));
        await Run(new EntitySearchRequest(EntitySearchAction.GetEntity, Id: id));
        await Run(new EntitySearchRequest(EntitySearchAction.GetFields, Id: id, Fields: ["Name"]));
        await Run(new EntitySearchRequest(EntitySearchAction.GetFieldsBatch,
            Items: [new EntityFieldsBatchItem(id, ["Name"])]));

        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchByType_MissingEntityType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.SearchByType, EntityType: null)));
        Assert.Contains("entity_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchByType_EmptyEntityType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.SearchByType, EntityType: [])));
        Assert.Contains("entity_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchByLogicalId_MissingLogicalId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.SearchByLogicalId, LogicalId: null)));
        Assert.Contains("logical_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchByLogicalId_MissingEntityType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.SearchByLogicalId, LogicalId: "1")));
        Assert.Contains("entity_type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEntity_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetEntity)));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFields_MissingFields_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFields,
                    Id: Guid.NewGuid(), Fields: null)));
        Assert.Contains("fields", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFields_MissingId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFields, Fields: ["Name"])));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFields_EmptyFields_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFields,
                    Id: Guid.NewGuid(), Fields: [])));
        Assert.Contains("fields", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFieldsBatch_MissingItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFieldsBatch, Items: null)));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFieldsBatch_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFieldsBatch, Items: [])));
        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFieldsBatch_OverLimit_ThrowsArgumentException()
    {
        var items = Enumerable.Range(0, 51)
            .Select(_ => new EntityFieldsBatchItem(Guid.NewGuid(), ["Name"]))
            .ToList();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest(EntitySearchAction.GetFieldsBatch, Items: items)));
        Assert.Contains("50", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAction_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            EntitySearchTool.Execute(TestHelpers.FakeManager(),
                new EntitySearchRequest((EntitySearchAction)999)));
        Assert.Contains("unknown action", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
