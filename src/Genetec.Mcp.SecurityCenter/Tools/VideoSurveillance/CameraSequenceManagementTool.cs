using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraSequenceManagementTool
{
    [McpServerTool(
        Name = "camera_sequence_management",
        Title = "Camera Sequence Management",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage camera sequences: get, list, update, and get_cameras. Camera membership is read-only through the WebSDK.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera sequence management request")] CameraSequenceManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case CameraSequenceAction.Get:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get'.");
                return await client.GetEntityTrimmed(request.Id.Value, "Sequence");

            case CameraSequenceAction.List:
                return await client.ListEntities("Sequence", request.PageSize, request.Page, returnFields: ["Name"]);

            case CameraSequenceAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<SequenceUpdateProperties>(client, items, "Sequence",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) },
                    name => new SequenceUpdateProperties(Name: name));
            }

            case CameraSequenceAction.GetCameras:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_cameras'.");
                await client.RequireEntityType(request.Id.Value, "id", "Sequence");
                return await client.GetEntityField(request.Id.ToString(), "Cameras");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum CameraSequenceAction
{
    Get,
    List,
    Update,
    GetCameras
}

public sealed record CameraSequenceManagementItem(
    [property: Description("Sequence name for rename"), MinLength(1)] string? Name = null,
    [property: Description("Sequence GUID for update")] Guid? Id = null,
    [property: Description("Typed Sequence properties for update")] SequenceUpdateProperties? Body = null) : IManagementCrudItem<SequenceUpdateProperties>;

public sealed record CameraSequenceManagementRequest(
    [property: Description("Action to perform"), Required] CameraSequenceAction Action,
    [property: Description("Camera sequence GUID for get or get_cameras")] Guid? Id = null,
    [property: Description("Max results per page for enumeration (default 50, range 1-100). Use small pages; do not fetch all entities automatically. Ask the user before large queries."), Range(1, ResourceLimits.MaxPageSize)] int PageSize = ResourceLimits.DefaultPageSize,
    [property: Description("Page number for enumeration (1-based)"), Range(1, int.MaxValue)] int Page = 1,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<CameraSequenceManagementItem>? Items = null);

public sealed record SequenceUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Entity description")] string? Description = null);
