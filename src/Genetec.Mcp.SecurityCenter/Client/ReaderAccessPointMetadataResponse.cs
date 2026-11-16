using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.Client;

internal sealed record ReaderAccessPointMetadataResponse : EntityMetadataResponse
{
    [JsonConverter(typeof(JsonStringEnumConverter<AccessPointType>))]
    public AccessPointType? AccessPointType { get; init; }
    public Guid? Device { get; init; }
}
