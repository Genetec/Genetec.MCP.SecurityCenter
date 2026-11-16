using Genetec.Mcp.SecurityCenter.UnitTests.Tools;
using Genetec.Mcp.SecurityCenter.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Administration;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Administration;

public class ScheduleManagementToolTests
{
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NullItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
                new ScheduleManagementRequest(ScheduleAction.Create, Items: null)));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_EmptyItems_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
                new ScheduleManagementRequest(ScheduleAction.Create, Items: [])));

        Assert.Contains("items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
                new ScheduleManagementRequest(ScheduleAction.Get)));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
                new ScheduleManagementRequest(ScheduleAction.Update, Items: [new ScheduleBatchItem(Name: "NewName")])));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_NullBodyAndName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
                new ScheduleManagementRequest(ScheduleAction.Update, Items: [new ScheduleBatchItem(Id: Guid.NewGuid())])));

        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../action")]
    [InlineData("../../action")]
    [InlineData("valid?unexpected=1")]
    [InlineData("%2e%2e%2faction")]
    public void Delete_PathSyntaxInItemId_RejectsProductionBinding(string id)
    {
        var json = JsonSerializer.Serialize(new { action = "delete", items = new[] { new { id } } });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScheduleManagementRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Theory]
    [InlineData("create", "{\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("create", "{\"name\":\" \"}")]
    [InlineData("create", "{\"name\":\"A\",\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("create", "null")]
    [InlineData("delete", "{\"name\":\"A\"}")]
    [InlineData("delete", "{}")]
    [InlineData("delete", "{\"name\":\"A\",\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("delete", "null")]
    public async Task Batch_InvalidItem_RejectsBeforeSdk(string action, string item)
    {
        var json = "{\"action\":\"" + action + "\",\"items\":[" + item + "]}";
        var request = JsonSerializer.Deserialize<ScheduleManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(), request));
        Assert.Equal("items", error.ParamName);
    }

    [Fact]
    public async Task Delete_TopLevelId_IgnoresIdAndValidatesItems()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
            new ScheduleManagementRequest(ScheduleAction.Delete, Id: Guid.NewGuid(), Items: [new ScheduleBatchItem()])));
        Assert.Equal("items", error.ParamName);
    }

    [Fact]
    public async Task Delete_Name_RejectsCreateInput()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
            new ScheduleManagementRequest(ScheduleAction.Delete, Items: [new ScheduleBatchItem(Name: "A", Id: Guid.NewGuid())])));
        Assert.Equal("items", error.ParamName);
    }

    [Fact]
    public async Task Create_TopLevelId_IgnoresIdAndValidatesItems()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(),
            new ScheduleManagementRequest(ScheduleAction.Create, Id: Guid.NewGuid(), Items: [new ScheduleBatchItem(Id: Guid.NewGuid())])));
        Assert.Equal("items", error.ParamName);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("delete")]
    public void Batch_TypedItems_BindsProductionRequest(string action)
    {
        var id = Guid.NewGuid();
        var json = action == "create"
            ? "{\"action\":\"create\",\"items\":[{\"name\":\"A\"}]}"
            : "{\"action\":\"delete\",\"items\":[{\"id\":\"" + id + "\"}]}";
        var request = JsonSerializer.Deserialize<ScheduleManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var item = Assert.Single(request.Items!);
        if (action == "create") Assert.Equal("A", item.Name);
        else Assert.Equal(id, item.Id);
    }

    [Fact]
    public void Request_RequiresAction_AndValidatesBatchLimit()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScheduleManagementRequest>("{}", NumericStringConverter.ToolOptions));
        var request = new ScheduleManagementRequest(ScheduleAction.Delete,
            Items: Enumerable.Range(0, ResourceLimits.MaxBatchItems + 1).Select(_ => new ScheduleBatchItem(Id: Guid.NewGuid())).ToList());
        var json = JsonSerializer.Serialize(request, NumericStringConverter.ToolOptions);
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ScheduleManagementRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void Discovery_ExposesNamedCreateItemsAndGuidDeleteItems()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "schedule_management", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), t => t.GetProperty("name").GetString() == "schedule_management");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var items = Assert.Single(request.GetProperty("fields").EnumerateArray(), f => f.GetProperty("name").GetString() == "items");
        var fields = items.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal("String", Assert.Single(fields, f => f.GetProperty("name").GetString() == "name").GetProperty("type").GetString());
        Assert.Equal("Guid", Assert.Single(fields, f => f.GetProperty("name").GetString() == "id").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(ScheduleAction.Get)]
    [InlineData(ScheduleAction.Update)]
    [InlineData(ScheduleAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectAndPreserveTarget(ScheduleAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = (action is ScheduleAction.Update or ScheduleAction.Delete ? new ScheduleManagementRequest(action, Items: [new ScheduleBatchItem(Id: id, Body: action == ScheduleAction.Update ? new ScheduleUpdateProperties(Name: "Must not update") : null)]) : new ScheduleManagementRequest(action, Id: id));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action is ScheduleAction.Update or ScheduleAction.Delete ? "items" : "id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Delete_WrongEntityTypeInItems_RejectsSingleAndBatch(int count)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = new ScheduleManagementRequest(ScheduleAction.Delete,
            Items: Enumerable.Range(0, count).Select(_ => new ScheduleBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ScheduleManagementTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(await ScheduleManagementTool.Execute(TestHelpers.FakeManager(handler), request)));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }
}
