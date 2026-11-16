namespace Genetec.Mcp.SecurityCenter.Client;

internal sealed record AccessPointMetadataResponse : EntityMetadataResponse
{
    public Guid? Door { get; init; }
}
