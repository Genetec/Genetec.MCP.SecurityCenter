using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.Utilities;

[McpServerToolType]
public class MapManagementTool
{
    public static Guid ResolveProviderGuid(MapProviderType mapProvider) => mapProvider switch
    {
        MapProviderType.Google => new("d7287da0-a7ff-405f-8166-b6baf26d066c"),
        MapProviderType.Esri or MapProviderType.Arcgis => new("2f3a8f7a-51f4-4558-970f-0521687b661e"),
        MapProviderType.Image => Guid.Empty,
        MapProviderType.GoogleTerrain => new("a42edf2e-63c5-4967-9dbf-4efb3af7bc11"),
        MapProviderType.Custom => new("34632cfb-e460-4fb6-a3b4-75d066422844"),
        _ => throw new ArgumentException("Parameter 'map_provider' must be a supported provider.", "map_provider")
    };

    [McpServerTool(
        Name = "map_management",
        Title = "Map Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage maps: create, get_maps, get_objects, add_object, remove_object. " +
                 "For create, specify map_provider or map_provider_id.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Map management request")] MapManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case MapAction.Create:
                if (request.AreaId is null) throw new ArgumentException("Parameter 'area_id' is required for action 'create'.");
                if (request.MapProvider.HasValue == request.MapProviderId.HasValue)
                    throw new ArgumentException("Specify exactly one of 'map_provider' and 'map_provider_id' for action 'create'.", "map_provider");
                var providerGuid = request.MapProvider.HasValue
                    ? ResolveProviderGuid(request.MapProvider.Value)
                    : request.MapProviderId!.Value;
                await client.RequireEntityType(request.AreaId.Value, "area_id", "Area");
                return await client.ExecuteEntityMethod(request.AreaId!.ToString(), "CreateMap",
                    $"GeographicalMapCreationInfo({providerGuid:D})");

            case MapAction.GetMaps:
                return await client.ListEntities("Map", request.PageSize, request.Page);

            case MapAction.GetObjects:
                if (request.MapId is null) throw new ArgumentException("Parameter 'map_id' is required for action 'get_objects'.");
                await client.RequireEntityType(request.MapId.Value, "map_id", "Map");
                return await client.GetEntityField(request.MapId!.ToString(), "MapObjects");

            case MapAction.AddObject:
                if (request.MapId is null) throw new ArgumentException("Parameter 'map_id' is required for action 'add_object'.");
                if (request.ObjectId is null) throw new ArgumentException("Parameter 'object_id' is required for action 'add_object'.");
                await client.RequireEntityType(request.MapId.Value, "map_id", "Map");
                await client.RequireEntityType(request.ObjectId.Value, "object_id", "Camera");
                return await client.ExecuteEntityMethod(
                    request.MapId!.ToString(),
                    "Add",
                    $"CameraMapObject(){{LinkedEntity={request.ObjectId!.ToString()}}}");

            case MapAction.RemoveObject:
                if (request.MapId is null) throw new ArgumentException("Parameter 'map_id' is required for action 'remove_object'.");
                if (request.ObjectId is null) throw new ArgumentException("Parameter 'object_id' is required for action 'remove_object'.");
                await client.RequireEntityType(request.MapId.Value, "map_id", "Map");
                return await client.ExecuteEntityMethod(request.MapId!.ToString(), "Remove", request.ObjectId!.ToString());

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum MapAction { Create, GetMaps, GetObjects, AddObject, RemoveObject }

public enum MapProviderType
{
    [Description("Google map provider")] Google,
    [Description("Esri map provider")] Esri,
    [Description("ArcGIS alias for the Esri map provider")] Arcgis,
    [Description("Image map provider")] Image,
    [Description("Google terrain map provider")] GoogleTerrain,
    [Description("Custom map provider")] Custom
}

public sealed record MapManagementRequest(
    [property: Description("Action to perform"), Required] MapAction Action,
    [property: Description("Parent area GUID (for create)")] Guid? AreaId = null,
    [property: Description("Known map provider for create; mutually exclusive with map_provider_id")] MapProviderType? MapProvider = null,
    [property: Description("Map GUID")] Guid? MapId = null,
    [property: Description("Linked entity GUID for add_object; map object GUID returned by get_objects for remove_object")] Guid? ObjectId = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("Provider GUID for create; mutually exclusive with map_provider")] Guid? MapProviderId = null);
