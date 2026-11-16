using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Lpr;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Lpr;

public class ParkingZoneToolTests
{
    [Theory]
    [InlineData(ParkingZoneAction.Create)]
    [InlineData(ParkingZoneAction.Update)]
    [InlineData(ParkingZoneAction.Delete)]
    public async Task Crud_MissingItems_RejectsBeforeSdk(ParkingZoneAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest(action)));
        Assert.Equal("items", error.ParamName);
    }

    [Theory]
    [InlineData(ParkingZoneAction.Create)]
    [InlineData(ParkingZoneAction.Update)]
    [InlineData(ParkingZoneAction.Delete)]
    public async Task Crud_TopLevelId_RejectsBeforeSdk(ParkingZoneAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest(action, Id: Guid.NewGuid(), Items: [new ParkingZoneBatchItem(Name: "A")])));
        Assert.Equal("id", error.ParamName);
    }

    [Fact]
    public async Task Get_MissingId_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest(ParkingZoneAction.Get)));
        Assert.Equal("id", error.ParamName);
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
    [InlineData("update", "{\"body\":{\"name\":\"A\"}}")]
    [InlineData("update", "{\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("update", "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"A\",\"body\":{\"name\":\"B\"}}")]
    [InlineData("update", "null")]
    public async Task Crud_InvalidItem_RejectsBeforeSdk(string action, string item)
    {
        var json = "{\"action\":\"" + action + "\",\"items\":[" + item + "]}";
        var request = JsonSerializer.Deserialize<ParkingZoneManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(), request));
        Assert.Equal("items", error.ParamName);
    }

    [Theory]
    [InlineData("../action")]
    [InlineData("../../action")]
    [InlineData("valid?unexpected=1")]
    [InlineData("%2e%2e%2faction")]
    public void Crud_PathSyntaxInItemId_RejectsProductionBinding(string id)
    {
        var json = JsonSerializer.Serialize(new { action = "delete", items = new[] { new { id } } });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("body")]
    public void Request_LegacyTopLevelInputs_RejectProductionBinding(string field)
    {
        var json = field == "name" ? "{\"action\":\"create\",\"name\":\"A\"}" : "{\"action\":\"update\",\"body\":{}}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public void Crud_TypedItems_BindsProductionRequest(string action)
    {
        var id = Guid.NewGuid();
        var json = action == "create"
            ? "{\"action\":\"create\",\"items\":[{\"name\":\"A\"}]}"
            : "{\"action\":\"" + action + "\",\"items\":[{\"id\":\"" + id + "\"" + (action == "update" ? ",\"body\":{\"name\":\"A\"}" : "") + "}]}";
        var request = JsonSerializer.Deserialize<ParkingZoneManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var item = Assert.Single(request.Items!);
        if (action == "create") Assert.Equal("A", item.Name);
        else Assert.Equal(id, item.Id);
        if (action == "update") Assert.Equal("A", item.Body!.Name);
    }

    [Fact]
    public void Request_RequiresActionAndValidatesItemsConstraints()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>("{}", NumericStringConverter.ToolOptions));
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>("{\"action\":\"create\",\"items\":[]}", NumericStringConverter.ToolOptions));
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>("{\"action\":\"create\",\"items\":[{\"name\":\"\"}]}", NumericStringConverter.ToolOptions));
        var request = new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items:
            Enumerable.Range(0, ResourceLimits.MaxBatchItems + 1).Select(_ => new ParkingZoneBatchItem(Id: Guid.NewGuid())).ToList());
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ParkingZoneManagementRequest>(
            JsonSerializer.Serialize(request, NumericStringConverter.ToolOptions), NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void Discovery_ExposesCrudInputsOnlyInsideItems()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "parking_zone", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), t => t.GetProperty("name").GetString() == "parking_zone");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var fields = request.GetProperty("fields").EnumerateArray().ToArray();
        Assert.DoesNotContain(fields, field => field.GetProperty("name").GetString() is "name" or "body");
        var items = Assert.Single(fields, field => field.GetProperty("name").GetString() == "items");
        var itemFields = items.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal("String", Assert.Single(itemFields, f => f.GetProperty("name").GetString() == "name").GetProperty("type").GetString());
        Assert.Equal("Guid", Assert.Single(itemFields, f => f.GetProperty("name").GetString() == "id").GetProperty("type").GetString());
        Assert.Contains(itemFields, f => f.GetProperty("name").GetString() == "body");
    }

    [Theory]
    [InlineData(ParkingZoneAction.Get)]
    [InlineData(ParkingZoneAction.Update)]
    [InlineData(ParkingZoneAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectWithoutMutation(ParkingZoneAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action == ParkingZoneAction.Get
            ? new ParkingZoneManagementRequest(action, Id: id)
            : new ParkingZoneManagementRequest(action, Items: [new ParkingZoneBatchItem(Id: id,
                Body: action == ParkingZoneAction.Update ? new ParkingZoneUpdateProperties(Name: "Must not update") : null)]);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action == ParkingZoneAction.Get ? "id" : "items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("Hotlist", "items.body.hotlist")]
    [InlineData("LprManager", "items.body.lpr_manager")]
    public async Task Update_WrongLinkedEntityType_RejectsBeforeMutation(string property, string parameter)
    {
        var id = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "ParkingZone");
        handler.AddEntity(wrongId, "UserGroup");
        var body = property switch
        {
            "Hotlist" => new ParkingZoneUpdateProperties(Hotlist: wrongId),
            "LprManager" => new ParkingZoneUpdateProperties(LprManager: wrongId),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(handler),
            new ParkingZoneManagementRequest(ParkingZoneAction.Update, Items: [new ParkingZoneBatchItem(Id: id, Body: body)])));
        Assert.Equal(parameter, error.ParamName);
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
        var request = new ParkingZoneManagementRequest(ParkingZoneAction.Delete,
            Items: Enumerable.Range(0, count).Select(_ => new ParkingZoneBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = JsonDocument.Parse((string)await ParkingZoneTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("Hotlist", 1)]
    [InlineData("Hotlist", 2)]
    [InlineData("LprManager", 1)]
    [InlineData("LprManager", 2)]
    public async Task Update_WrongLinkedTypeInItems_RejectsSingleAndBatch(string property, int count)
    {
        var id = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "ParkingZone");
        handler.AddEntity(wrongId, "UserGroup");
        var body = property switch
        {
            "Hotlist" => new ParkingZoneUpdateProperties(Hotlist: wrongId),
            "LprManager" => new ParkingZoneUpdateProperties(LprManager: wrongId),
            _ => throw new InvalidOperationException()
        };
        var request = new ParkingZoneManagementRequest(ParkingZoneAction.Update,
            Items: Enumerable.Range(0, count).Select(_ => new ParkingZoneBatchItem(Id: id, Body: body)).ToList());
        if (count == 1)
            await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(handler), request));
        else
        {
            using var document = JsonDocument.Parse((string)await ParkingZoneTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Delete_Body_RejectsMutationInput()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest(ParkingZoneAction.Delete, Items: [new ParkingZoneBatchItem(Id: Guid.NewGuid(), Body: new ParkingZoneUpdateProperties())])));
        Assert.Equal("items.body", error.ParamName);
    }

    [Fact]
    public async Task UnknownAction_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest((ParkingZoneAction)int.MaxValue)));
        Assert.Equal("action", error.ParamName);
    }

    [Fact]
    public async Task Create_Body_RejectsUpdateInput()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ParkingZoneTool.Execute(TestHelpers.FakeManager(),
            new ParkingZoneManagementRequest(ParkingZoneAction.Create, Items: [new ParkingZoneBatchItem(Name: "A", Body: new ParkingZoneUpdateProperties())])));
        Assert.Equal("items.body", error.ParamName);
    }
}
