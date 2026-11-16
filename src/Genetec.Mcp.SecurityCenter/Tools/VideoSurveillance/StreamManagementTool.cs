using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using Genetec.Mcp.SecurityCenter.Tools;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

public enum StreamManagementAction { Get, ListByCamera, Update }

public sealed record StreamManagementItem(
    [property: Description("Stream GUID for update")] Guid? Id = null,
    [property: Description("Typed Stream properties for update")] StreamUpdateProperties? Body = null) : IManagementCrudItem<StreamUpdateProperties>
{
    string? IManagementBatchItem.Name => null;
}

public sealed record StreamManagementRequest(
    [property: Description("Action to perform"), Required] StreamManagementAction Action,
    [property: Description("Stream GUID for get")] Guid? Id = null,
    [property: Description("Camera GUID (for list_by_camera)")] Guid? CameraId = null,
    [property: Description("One or more items to update"), MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<StreamManagementItem>? Items = null);

[McpServerToolType]
public class StreamManagementTool
{
    [McpServerTool(
        Name = "stream_management",
        Title = "Stream Management",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true),
     Description("Manage streams: get, list_by_camera, update. " +
                 "Use stream_configuration to change encoding parameters.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Stream management request")] StreamManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        switch (request.Action)
        {
            case StreamManagementAction.Get:
                if (request.Id == null)
                    throw new ArgumentException("Parameter 'id' is required for get.");
                return await client.GetEntity(request.Id.Value, "Stream");

            case StreamManagementAction.ListByCamera:
                if (request.CameraId == null)
                    throw new ArgumentException("Parameter 'camera_id' is required for list_by_camera.");
                await client.RequireEntityType(request.CameraId.Value, "camera_id", "Camera");
                return await client.GetEntityField(request.CameraId!.ToString(), "Streams");

            case StreamManagementAction.Update:
            {
                var items = ManagementToolHelper.RequireItems(request.Items);
                return await ManagementToolHelper.Update<StreamUpdateProperties>(client, items, "Stream",
                    body => body with { Name = ManagementToolHelper.SanitizeUpdateName(body.Name) });
            }

            default:
                throw new ArgumentException($"Unknown action: {request.Action}");
        }
    }
}

public sealed record StreamUpdateProperties(
    [Description("Entity name")] string? Name = null,
    [Description("Stream block size")] int? BlockSize = null,
    [Description("Video device GUID")] Guid? Device = null,
    [Description("Whether the stream is present")] bool? IsPresent = null,
    [Description("Legacy stream configuration")] string? LegacyConfigurations = null,
    [Description("IPv4 multicast address")] string? MulticastAddress = null,
    [Description("IPv6 multicast address")] string? MulticastAddressIPv6 = null,
    [Description("IPv4 multicast port")] int? MulticastPort = null,
    [Description("IPv6 multicast port")] int? MulticastPortIPv6 = null,
    [Description("Stream number")] int? Number = null,
    [Description("Stream type")] string? StreamType = null,
    [Description("Supported stream connection type")] string? SupportedConnection = null,
    [Description("Supported block size")] string? SupportedBlockSize = null,
    [Description("Configured connection type")] string? ConfiguredConnection = null,
    [Description("Stream capabilities")] string? Capabilities = null,
    [Description("UDP port")] int? UDPPort = null);
