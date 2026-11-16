using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Client;

internal sealed record AlarmAttachedEntitiesResponse
{
    [JsonRequired]
    public List<Guid> AttachedEntities { get; init; } = null!;
}
