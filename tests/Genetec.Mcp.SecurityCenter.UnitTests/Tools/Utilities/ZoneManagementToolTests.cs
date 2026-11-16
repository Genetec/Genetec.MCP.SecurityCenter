using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class ZoneManagementToolTests
{
    [Theory]
    [InlineData(ZoneAction.Create)]
    [InlineData(ZoneAction.Update)]
    [InlineData(ZoneAction.Delete)]
    public async Task Crud_MissingItems_RejectsBeforeSdk(ZoneAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest(action)));
        Assert.Equal("items", error.ParamName);
    }

    [Theory]
    [InlineData(ZoneAction.Create)]
    [InlineData(ZoneAction.Update)]
    [InlineData(ZoneAction.Delete)]
    public async Task Crud_TopLevelId_RejectsBeforeSdk(ZoneAction action)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest(action, Id: Guid.NewGuid(), Items: [new ZoneBatchItem(Name: "A")])));
        Assert.Equal("id", error.ParamName);
    }

    [Fact]
    public async Task Get_MissingId_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest(ZoneAction.Get)));
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
        var request = JsonSerializer.Deserialize<ZoneManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(), request));
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
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>(json, NumericStringConverter.ToolOptions));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("body")]
    public void Request_LegacyTopLevelInputs_RejectProductionBinding(string field)
    {
        var json = field == "name" ? "{\"action\":\"create\",\"name\":\"A\"}" : "{\"action\":\"update\",\"body\":{}}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>(json, NumericStringConverter.ToolOptions));
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
        var request = JsonSerializer.Deserialize<ZoneManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var item = Assert.Single(request.Items!);
        if (action == "create") Assert.Equal("A", item.Name);
        else Assert.Equal(id, item.Id);
        if (action == "update") Assert.Equal("A", item.Body!.Name);
    }

    [Fact]
    public void Request_RequiresActionAndValidatesItemsConstraints()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>("{}", NumericStringConverter.ToolOptions));
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>("{\"action\":\"create\",\"items\":[]}", NumericStringConverter.ToolOptions));
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>("{\"action\":\"create\",\"items\":[{\"name\":\"\"}]}", NumericStringConverter.ToolOptions));
        var request = new ZoneManagementRequest(ZoneAction.Delete, Items:
            Enumerable.Range(0, ResourceLimits.MaxBatchItems + 1).Select(_ => new ZoneBatchItem(Id: Guid.NewGuid())).ToList());
        Assert.Throws<ValidationException>(() => JsonSerializer.Deserialize<ZoneManagementRequest>(
            JsonSerializer.Serialize(request, NumericStringConverter.ToolOptions), NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void Discovery_ExposesCrudInputsOnlyInsideItems()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "zone_management", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), t => t.GetProperty("name").GetString() == "zone_management");
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
    [InlineData(ZoneAction.Get)]
    [InlineData(ZoneAction.Update)]
    [InlineData(ZoneAction.Delete)]
    public async Task TargetActions_WrongEntityType_RejectWithoutMutation(ZoneAction action)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var request = action == ZoneAction.Get
            ? new ZoneManagementRequest(action, Id: id)
            : new ZoneManagementRequest(action, Items: [new ZoneBatchItem(Id: id,
                Body: action == ZoneAction.Update ? new ZoneUpdateProperties(Name: "Must not update") : null)]);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Equal(action == ZoneAction.Get ? "id" : "items", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("AccessControlUnit", "items.body.access_control_unit")]
    [InlineData("InterfaceModule", "items.body.interface_module")]
    [InlineData("Owner", "items.body.owner")]
    public async Task Update_WrongLinkedEntityType_RejectsBeforeMutation(string property, string parameter)
    {
        var id = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Zone");
        handler.AddEntity(wrongId, "UserGroup");
        var body = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(handler),
            new ZoneManagementRequest(ZoneAction.Update, Items: [new ZoneBatchItem(Id: id, Body: body)])));
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
        var request = new ZoneManagementRequest(ZoneAction.Delete,
            Items: Enumerable.Range(0, count).Select(_ => new ZoneBatchItem(Id: id)).ToList());
        if (count == 1)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal("items", error.ParamName);
        }
        else
        {
            using var document = JsonDocument.Parse((string)await ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("AccessControlUnit", 1)]
    [InlineData("AccessControlUnit", 2)]
    [InlineData("InterfaceModule", 1)]
    [InlineData("InterfaceModule", 2)]
    [InlineData("Owner", 1)]
    [InlineData("Owner", 2)]
    public async Task Update_WrongLinkedTypeInItems_RejectsSingleAndBatch(string property, int count)
    {
        var id = Guid.NewGuid();
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "Zone");
        handler.AddEntity(wrongId, "UserGroup");
        var body = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),
            _ => throw new InvalidOperationException()
        };
        var request = new ZoneManagementRequest(ZoneAction.Update,
            Items: Enumerable.Range(0, count).Select(_ => new ZoneBatchItem(Id: id, Body: body)).ToList());
        if (count == 1)
            await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        else
        {
            using var document = JsonDocument.Parse((string)await ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
            Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("failure_count").GetInt32());
        }
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("AccessControlUnit", "items.body.access_control_unit")]
    [InlineData("InterfaceModule", "items.body.interface_module")]
    [InlineData("Owner", "items.body.owner")]
    public async Task Create_WrongLinkedType_RejectsBeforeCreatingZone(string property, string parameter)
    {
        var wrongId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(wrongId, "UserGroup");
        var body = property switch
        {
            "AccessControlUnit" => new ZoneUpdateProperties(AccessControlUnit: wrongId),
            "InterfaceModule" => new ZoneUpdateProperties(InterfaceModule: wrongId),
            "Owner" => new ZoneUpdateProperties(Owner: wrongId),
            _ => throw new InvalidOperationException()
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(handler),
            new ZoneManagementRequest(ZoneAction.Create, Items: [new ZoneBatchItem(Name: "Must not create", Body: body)])));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData(ZoneAction.Create)]
    [InlineData(ZoneAction.Update)]
    public async Task AccessControlUnit_ValidUnit_PassesGuardBeforeMutation(ZoneAction action)
    {
        var id = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var upstreamError = new HttpRequestException("Mutation reached.");
        var fallback = new ToolTestHandler { ResponseFactory = _ => throw upstreamError };
        var handler = new EntityMetadataTestHandler(fallback);
        handler.AddEntity(id, "Zone");
        handler.AddEntity(unitId, "Unit");
        var json = JsonSerializer.Serialize(new
        {
            action = action.ToString().ToLowerInvariant(),
            items = new[] { new
            {
                id = action == ZoneAction.Update ? (Guid?)id : null,
                name = action == ZoneAction.Create ? "MCP_Zone_Unit" : null,
                body = new { access_control_unit = unitId }
            } }
        });
        var request = JsonSerializer.Deserialize<ZoneManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            ZoneManagementTool.Execute(TestHelpers.FakeManager(handler), request));
        Assert.Same(upstreamError, error);
        var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains($"AccessControlUnit={unitId}", Uri.UnescapeDataString(mutation.Uri.Query));
    }

    [Fact]
    public async Task Delete_Body_RejectsMutationInput()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest(ZoneAction.Delete, Items: [new ZoneBatchItem(Id: Guid.NewGuid(), Body: new ZoneUpdateProperties())])));
        Assert.Equal("items.body", error.ParamName);
    }

    [Fact]
    public async Task UnknownAction_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest((ZoneAction)int.MaxValue)));
        Assert.Equal("action", error.ParamName);
    }

    [Fact]
    public async Task Update_NameOnly_RequiresTypedBody()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ZoneManagementTool.Execute(TestHelpers.FakeManager(),
            new ZoneManagementRequest(ZoneAction.Update, Items: [new ZoneBatchItem(Id: Guid.NewGuid(), Name: "A")])));
        Assert.Equal("items", error.ParamName);
    }
}
