using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class VideoUnitGetTool
{
    public sealed record VideoUnitGetRequest(
        [property: Description("Video unit GUID"), Required] Guid Id);

    [McpServerTool(
        Name = "video_unit_get",
        Title = "Get Video Unit",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Retrieve full details of a single video unit in Security Center by GUID, " +
        "including name, type, and configuration.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Video unit get request")] VideoUnitGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);
        return await client.GetEntityTrimmed(request.Id, "VideoUnit");
    }
}
