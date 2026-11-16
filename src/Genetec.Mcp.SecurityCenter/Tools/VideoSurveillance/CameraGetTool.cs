using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class CameraGetTool
{
    public sealed record CameraGetRequest(
        [property: Description("Camera GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "camera_get",
        Title = "Get Camera",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single camera in Security Center by GUID, " +
        "including name, entity type, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Camera get request")] CameraGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "Camera");
    }
}
