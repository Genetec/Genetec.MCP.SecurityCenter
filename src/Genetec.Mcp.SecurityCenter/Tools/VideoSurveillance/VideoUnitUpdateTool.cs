using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Helpers;
using ModelContextProtocol.Server;

namespace Genetec.Mcp.SecurityCenter.Tools.VideoSurveillance;

[McpServerToolType]
public class VideoUnitUpdateTool
{
    [McpServerTool(
        Name = "video_unit_update",
        Title = "Update Video Unit",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true),
     Description(
        "Update one or more video units in Security Center using typed WebSDK properties. " +
        "Provide a single item to update one unit, or multiple items for a batch update.")]
    public static async Task<object> Execute(
        SecurityCenterSessionManager sessionManager,
        [Description("Video unit update request")] VideoUnitUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = sessionManager.GetClient(cancellationToken);

        if (request.Items.Count == 1)
        {
            var item = request.Items[0];
            await client.RequireEntityType(item.Id, "items", "VideoUnit");
            return await client.UpdateEntity(item.Id.ToString(), item);
        }

        var pairs = request.Items
            .Select(i => (i.Id, (object)i))
            .ToList();
        return await client.UpdateEntitiesBatch(pairs, "VideoUnit");
    }
}

public sealed record VideoUnitUpdateRequest(
    [property: Description("One or more video units to update"), Required, MinLength(1), MaxLength(ResourceLimits.MaxBatchItems)] List<VideoUnitUpdateItem> Items);

public sealed record VideoUnitUpdateItem(
    [property: Description("Video unit GUID"), Required] Guid Id,
    [Description("New name")] string? Name = null,
    [Description("Video unit username")] string? Username = null,
    [Description("Video unit password")] string? Password = null,
    [Description("Authentication scheme")] int? AuthenticationScheme = null,
    [Description("Certificate validation information")] string? CertificateValidationInfo = null,
    [Description("Command port")] int? CommandPort = null,
    [Description("Connection address")] string? ConnectionAddress = null,
    [Description("Whether DHCP is enabled")] bool? Dhcp = null,
    [Description("Discovery port")] int? DiscoveryPort = null,
    [Description("Firmware upgrade port")] int? FirmwareUpgradePort = null,
    [Description("First-add feature set")] string? FirstAddFeatureSet = null,
    [Description("Gateway")] string? Gateway = null,
    [Description("Hardware version")] string? HardwareVersion = null,
    [Description("Host name")] string? HostName = null,
    [Description("Legacy configurations")] string? LegacyConfigurations = null,
    [Description("Network prefix length")] int? PrefixLength = null,
    [Description("Proxy identifier")] string? ProxyIdentifier = null,
    [Description("Whether the connection is secure")] bool? SecureConnection = null,
    [Description("Subnet mask")] string? SubnetMask = null,
    [Description("Whether default credentials are used")] bool? UseDefaultCredentials = null,
    [Description("Video unit capabilities")] string? VideoUnitCapabilities = null,
    [Description("Web page")] string? WebPage = null);
