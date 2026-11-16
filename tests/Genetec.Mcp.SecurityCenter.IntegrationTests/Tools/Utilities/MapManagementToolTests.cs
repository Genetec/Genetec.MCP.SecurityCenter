using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;
using Genetec.Mcp.SecurityCenter.Tools.Utilities;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.Utilities;

public class MapManagementToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task WrongTargetType_RejectsAndPreservesUserGroup()
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        foreach (var action in new[] { MapAction.Create, MapAction.GetObjects, MapAction.AddObject, MapAction.RemoveObject })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(sc.Manager,
                new MapManagementRequest(action, AreaId: wrong.Id, MapProvider: MapProviderType.Google, MapId: wrong.Id, ObjectId: Guid.NewGuid())));
            Assert.Equal(action == MapAction.Create ? "area_id" : "map_id", error.ParamName);
        }
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    // ── Integration ─────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_MissingOrConflictingProviders_RejectsWithoutCreatingMap(bool supplyBoth)
    {
        RequireSecurityCenter();
        var area = Assert.IsType<EntityCreateResult>(await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_MapProviders_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(area.Guid);
        var before = await sc.Manager.GetClient().GetEntityFields(area.Guid.ToString(), "Guid", "Name", "MapId");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => MapManagementTool.Execute(sc.Manager,
            new MapManagementRequest(MapAction.Create, AreaId: area.Guid,
                MapProvider: supplyBoth ? MapProviderType.Google : null,
                MapProviderId: supplyBoth ? Guid.NewGuid() : null)));
        Assert.Equal("map_provider", error.ParamName);
        Assert.Equal(before, await sc.Manager.GetClient().GetEntityFields(area.Guid.ToString(), "Guid", "Name", "MapId"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SupportedActions_CreateListGetObjectsAddRemove_ReadBackState(bool useKnownProvider)
    {
        RequireSecurityCenter();

        var areaResult = await AreaCreateTool.Execute(sc.Manager,
            new AreaCreateRequest([new AreaItem($"MCP_MapManagement_{Guid.NewGuid():N}")]));
        var area = Assert.IsType<EntityCreateResult>(areaResult);
        sc.TrackForCleanup(area.Guid);

        Guid? mapId = null;
        var mapCreated = false;
        try
        {
            var cameraId = await GetFirstCameraIdAsync();

            var mapsBeforeCreate = Assert.IsType<string>(await Execute(MapAction.GetMaps));
            Assert.NotEmpty(ExtractEntityGuids(mapsBeforeCreate));

            var createResult = await Execute(MapAction.Create,
                areaId: area.Guid,
                mapProvider: useKnownProvider ? MapProviderType.Google : null,
                mapProviderId: useKnownProvider ? null : Guid.Parse("d7287da0-a7ff-405f-8166-b6baf26d066c"));
            mapCreated = true;
            var createdMapJson = Assert.IsType<string>(createResult);
            mapId = ExtractCreatedMapGuid(createdMapJson);
            Assert.NotEqual(Guid.Empty, mapId.Value);
            using var mapReadback = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().GetEntity(mapId.Value.ToString()));
            var map = IntegrationTestHelpers.GetResult(mapReadback.RootElement);
            Assert.Equal(mapId.Value, map.GetProperty("Guid").GetGuid());
            Assert.Equal("Map", map.GetProperty("EntityType").GetString());
            Assert.Equal(Guid.Parse("d7287da0-a7ff-405f-8166-b6baf26d066c"), map.GetProperty("ProviderId").GetGuid());

            var mapsAfterCreate = Assert.IsType<string>(await Execute(MapAction.GetMaps));
            Assert.Contains(mapId.Value, ExtractEntityGuids(mapsAfterCreate));

            var objectsBeforeAdd = await Execute(MapAction.GetObjects, mapId: mapId.Value);
            Assert.Empty(ExtractMapObjects(Assert.IsType<string>(objectsBeforeAdd)));

            var invalidObject = await Assert.ThrowsAsync<ArgumentException>(() => Execute(MapAction.AddObject,
                mapId: mapId.Value, objectId: area.Guid));
            Assert.Equal("object_id", invalidObject.ParamName);
            Assert.Empty(ExtractMapObjects(Assert.IsType<string>(await Execute(MapAction.GetObjects, mapId: mapId.Value))));

            var addResult = await Execute(MapAction.AddObject,
                mapId: mapId.Value,
                objectId: cameraId);
            AssertSdkResponse(addResult);

            var objectsAfterAdd = await Execute(MapAction.GetObjects, mapId: mapId.Value);
            var addedObject = Assert.Single(
                ExtractMapObjects(Assert.IsType<string>(objectsAfterAdd))
                    .Where(item => item.LinkedEntity == cameraId));

            var removeResult = await Execute(MapAction.RemoveObject,
                mapId: mapId.Value,
                objectId: addedObject.Id);
            AssertSdkResponse(removeResult);

            var objectsAfterRemove = await Execute(MapAction.GetObjects, mapId: mapId.Value);
            Assert.Empty(ExtractMapObjects(Assert.IsType<string>(objectsAfterRemove)));
        }
        finally
        {
            if (mapCreated || mapId.HasValue)
            {
                await sc.Manager.GetClient().ExecuteEntityMethod(
                    area.Guid.ToString(),
                    "DeleteMap");
            }
        }
    }

    private async Task<object> Execute(
        MapAction action,
        Guid? areaId = null,
        MapProviderType? mapProvider = null,
        Guid? mapId = null,
        Guid? objectId = null,
        Guid? mapProviderId = null)
        => await MapManagementTool.Execute(sc.Manager,
            new MapManagementRequest(action, areaId, mapProvider, mapId, objectId, MapProviderId: mapProviderId));

    private async Task<Guid> GetFirstCameraIdAsync()
    {
        var camerasJson = await sc.Manager.GetClient().ListEntities("Camera");
        var cameraId = ScIntegrationFixture.ExtractFirstGuid(camerasJson);
        Assert.True(cameraId.HasValue, "No camera is configured for map integration tests.");
        return cameraId.Value;
    }

    private void RequireSecurityCenter()
        => Assert.True(sc.IsAvailable,
            "Security Center integration configuration is required; configure appsettings.test.json or SC_HOST.");

    private static Guid ExtractCreatedMapGuid(string json)
    {
        using var document = JsonDocument.Parse(json);
        var map = document.RootElement.GetProperty("CreateMap");
        return map.GetProperty("Guid").GetGuid();
    }

    private static IReadOnlyList<Guid> ExtractEntityGuids(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var rows = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("Body", out var body) ? body : default;

        if (rows.ValueKind != JsonValueKind.Array)
            return [];

        return rows.EnumerateArray()
            .Select(row => row.TryGetProperty("Guid", out var guid) && guid.TryGetGuid(out var parsed)
                ? parsed
                : Guid.Empty)
            .Where(guid => guid != Guid.Empty)
            .ToList();
    }

    private static IReadOnlyList<MapObjectSnapshot> ExtractMapObjects(string json)
    {
        using var document = JsonDocument.Parse(json);
        var objects = document.RootElement.GetProperty("MapObjects");

        return objects.EnumerateArray()
            .Select(mapObject => new MapObjectSnapshot(
                mapObject.GetProperty("Id").GetGuid(),
                mapObject.GetProperty("LinkedEntity").GetGuid()))
            .ToList();
    }

    private static void AssertSdkResponse(object result)
        => Assert.IsType<string>(result);

    private sealed record MapObjectSnapshot(Guid Id, Guid LinkedEntity);
}
