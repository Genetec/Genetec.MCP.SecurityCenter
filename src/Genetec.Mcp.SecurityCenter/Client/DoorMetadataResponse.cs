namespace Genetec.Mcp.SecurityCenter.Client;

internal sealed record DoorMetadataResponse : EntityMetadataResponse
{
    public List<Guid>? DoorSides { get; init; }
}
