using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class ArchiverManagementTool
{
    [McpServerTool(
        Name = "archiver_management",
        Title = "Archiver Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
        Description("Manage archiver roles: get_cameras, get_agents, move_video_unit. " +
                 "get_agents returns agent GUIDs from ArchiveSources; use entity_search.get_fields " +
                 "with those GUIDs to read AgentType, RoleId, and ServerId. " +
                 "Moving uses the documented MoveVideoUnit action.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Archiver management request")] ArchiverManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case ArchiverAction.GetCameras:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_cameras'.");
                await client.RequireEntityType(request.Id.Value, "id", "Role");
                return await client.GetEntityField(request.Id!.ToString(), "Cameras");

            case ArchiverAction.GetAgents:
                if (request.Id is null) throw new ArgumentException("Parameter 'id' is required for action 'get_agents'.", "id");
                await client.RequireEntityType(request.Id.Value, "id", "Role");
                return await client.GetEntityField(request.Id.Value.ToString(), "ArchiveSources");

            case ArchiverAction.MoveVideoUnit:
                if (request.VideoUnitId is null)
                    throw new ArgumentException("Parameter 'video_unit_id' is required for action 'move_video_unit'.");
                if (request.TargetArchiverId is null)
                    throw new ArgumentException("Parameter 'target_archiver_id' is required for action 'move_video_unit'.");
                await client.RequireEntityType(request.VideoUnitId.Value, "video_unit_id", "VideoUnit");
                await client.RequireEntityType(request.TargetArchiverId.Value, "target_archiver_id", "Role");
                return await client.ExecuteAction(
                    $"MoveVideoUnit({request.VideoUnitId.Value},{request.TargetArchiverId.Value})");

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public enum ArchiverAction
{
    GetCameras,
    GetAgents,
    MoveVideoUnit
}

public sealed record ArchiverManagementRequest(
    [property: Description("Action to perform"), Required] ArchiverAction Action,
    [property: Description("Archiver role GUID")] Guid? Id = null,
    [property: Description("Video unit GUID (for move_video_unit)")] Guid? VideoUnitId = null,
    [property: Description("Destination archiver GUID (for move_video_unit)")] Guid? TargetArchiverId = null);
