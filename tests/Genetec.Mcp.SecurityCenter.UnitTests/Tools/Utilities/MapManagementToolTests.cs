using System.Text.Json;
using Genetec.Mcp.SecurityCenter.JsonConverters;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;
using Genetec.Mcp.SecurityCenter.Tools;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.Utilities;

public class MapManagementToolTests
{
    [Theory]
    [InlineData(MapAction.Create, "area_id")]
    [InlineData(MapAction.GetObjects, "map_id")]
    [InlineData(MapAction.AddObject, "map_id")]
    [InlineData(MapAction.RemoveObject, "map_id")]
    public async Task Target_WrongEntityType_RejectsBeforeMutation(MapAction action, string parameter)
    {
        var id = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(id, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MapManagementRequest(action, AreaId: id, MapProvider: MapProviderType.Google, MapId: id, ObjectId: Guid.NewGuid())));
        Assert.Equal(parameter, error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task AddObject_WrongLinkedEntityType_RejectsBeforeMutation()
    {
        var mapId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler();
        handler.AddEntity(mapId, "Map");
        handler.AddEntity(objectId, "UserGroup");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(TestHelpers.FakeManager(handler),
            new MapManagementRequest(MapAction.AddObject, MapId: mapId, ObjectId: objectId)));
        Assert.Equal("object_id", error.ParamName);
        Assert.DoesNotContain(handler.Requests, request => request.Method != HttpMethod.Get);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_MissingAreaId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MapManagementTool.Execute(TestHelpers.FakeManager(),
                new MapManagementRequest(MapAction.Create, MapProvider: MapProviderType.Google)));
        Assert.Contains("area_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingMapProvider_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MapManagementTool.Execute(TestHelpers.FakeManager(),
                new MapManagementRequest(MapAction.Create, AreaId: Guid.NewGuid())));
        Assert.Contains("map_provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MapAction.GetObjects)]
    [InlineData(MapAction.AddObject)]
    [InlineData(MapAction.RemoveObject)]
    public async Task MapIdRequiredActions_MissingMapId_ThrowsArgumentException(MapAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MapManagementTool.Execute(TestHelpers.FakeManager(),
                new MapManagementRequest(action)));
        Assert.Contains("map_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MapAction.AddObject)]
    [InlineData(MapAction.RemoveObject)]
    public async Task ObjectIdRequiredActions_MissingObjectId_ThrowsArgumentException(MapAction action)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MapManagementTool.Execute(TestHelpers.FakeManager(),
                new MapManagementRequest(action, MapId: Guid.NewGuid())));
        Assert.Contains("object_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Provider resolution ─────────────────────────────────────────

    [Theory]
    [InlineData(MapProviderType.Google, "d7287da0-a7ff-405f-8166-b6baf26d066c")]
    [InlineData(MapProviderType.Esri, "2f3a8f7a-51f4-4558-970f-0521687b661e")]
    [InlineData(MapProviderType.Arcgis, "2f3a8f7a-51f4-4558-970f-0521687b661e")]
    [InlineData(MapProviderType.Image, "00000000-0000-0000-0000-000000000000")]
    [InlineData(MapProviderType.GoogleTerrain, "a42edf2e-63c5-4967-9dbf-4efb3af7bc11")]
    [InlineData(MapProviderType.Custom, "34632cfb-e460-4fb6-a3b4-75d066422844")]
    public void ResolveProviderGuid_KnownProvider_ReturnsCorrectGuid(MapProviderType input, string expected)
    {
        Assert.Equal(Guid.Parse(expected), MapManagementTool.ResolveProviderGuid(input));
    }

    [Theory]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE")]
    public void ProviderId_BindsProductionRequest(string rawGuid)
    {
        var request = JsonSerializer.Deserialize<MapManagementRequest>(
            $$"""{"action":"create","map_provider_id":"{{rawGuid}}"}""", NumericStringConverter.ToolOptions)!;
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), request.MapProviderId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown_provider")]
    [InlineData("google)),Name=Injected")]
    [InlineData("d7287da0-a7ff-405f-8166-b6baf26d066c")]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee)),Name=Injected")]
    public void InvalidProvider_RejectsDuringBinding(string provider)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MapManagementRequest>(
            $$"""{"action":"create","map_provider":"{{provider}}"}""", NumericStringConverter.ToolOptions));
    }

    [Theory]
    [InlineData("google", MapProviderType.Google)]
    [InlineData("Google", MapProviderType.Google)]
    [InlineData("esri", MapProviderType.Esri)]
    [InlineData("arcgis", MapProviderType.Arcgis)]
    [InlineData("image", MapProviderType.Image)]
    [InlineData("google_terrain", MapProviderType.GoogleTerrain)]
    [InlineData("custom", MapProviderType.Custom)]
    public void ProviderAlias_BindsProductionRequest(string provider, MapProviderType expected)
    {
        var id = Guid.NewGuid();
        var json = $$"""{"action":"create","area_id":"{{id}}","map_provider":"{{provider}}"}""";
        var request = JsonSerializer.Deserialize<MapManagementRequest>(json, NumericStringConverter.ToolOptions)!;
        Assert.Equal(expected, request.MapProvider);
        Assert.Equal(id, request.AreaId);
    }

    [Fact]
    public async Task Create_BothProviderInputs_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(TestHelpers.FakeManager(),
            new MapManagementRequest(MapAction.Create, AreaId: Guid.NewGuid(), MapProvider: MapProviderType.Google,
                MapProviderId: Guid.NewGuid())));
        Assert.Equal("map_provider", error.ParamName);
        Assert.Contains("exactly one", error.Message);
    }

    [Fact]
    public async Task Create_InvalidEnum_RejectsBeforeSdk()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(TestHelpers.FakeManager(),
            new MapManagementRequest(MapAction.Create, AreaId: Guid.NewGuid(), MapProvider: (MapProviderType)999)));
        Assert.Equal("map_provider", error.ParamName);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}")]
    [InlineData("aaaaaaaabbbbccccddddeeeeeeeeeeee")]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee)),Name=Injected")]
    public void InvalidProviderId_RejectsDuringBinding(string providerId)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MapManagementRequest>(
            $$"""{"action":"create","map_provider_id":"{{providerId}}"}""", NumericStringConverter.ToolOptions));
    }

    [Fact]
    public void ImageProviderId_BindsEmptyGuid()
    {
        var request = JsonSerializer.Deserialize<MapManagementRequest>(
            """{"action":"create","map_provider_id":"00000000-0000-0000-0000-000000000000"}""", NumericStringConverter.ToolOptions)!;
        Assert.Equal(Guid.Empty, request.MapProviderId);
    }

    [Fact]
    public void Discovery_ExposesProviderEnumAndProviderGuid()
    {
        var result = SearchToolsTool.Execute(new SearchToolsRequest(Query: "map_management", Detail: ToolDetailLevel.Full));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, NumericStringConverter.ToolOptions));
        var tool = Assert.Single(document.RootElement.EnumerateArray(), t => t.GetProperty("name").GetString() == "map_management");
        var request = Assert.Single(tool.GetProperty("parameters").EnumerateArray());
        var fields = request.GetProperty("fields").EnumerateArray().ToArray();
        var provider = Assert.Single(fields, f => f.GetProperty("name").GetString() == "map_provider");
        Assert.Equal("MapProviderType", provider.GetProperty("type").GetString());
        Assert.Equal(new[] { "google", "esri", "arcgis", "image", "google_terrain", "custom" },
            provider.GetProperty("enum_values").EnumerateArray().Select(value => value.GetString()));
        var providerId = Assert.Single(fields, f => f.GetProperty("name").GetString() == "map_provider_id");
        Assert.Equal("Guid", providerId.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(provider.GetProperty("description").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(providerId.GetProperty("description").GetString()));
    }
}
