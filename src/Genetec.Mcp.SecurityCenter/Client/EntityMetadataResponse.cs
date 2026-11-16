namespace Genetec.Mcp.SecurityCenter.Client;

internal record EntityMetadataResponse
{
    public Guid Guid { get; init; }
    public string? EntityType { get; init; }
}
