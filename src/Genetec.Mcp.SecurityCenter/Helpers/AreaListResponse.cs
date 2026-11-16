using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Helpers;

internal sealed record AreaListEntry(
    [property: JsonPropertyName("Guid"), JsonRequired] Guid Guid,
    [property: JsonPropertyName("Name"), JsonRequired] string Name,
    [property: JsonPropertyName("HiddenFromUI"), JsonRequired] string HiddenFromUI,
    [property: JsonPropertyName("IsExternalSystemRootArea"), JsonRequired] string IsExternalSystemRootArea)
{
    // ReturnFields projects these Boolean properties as True/False strings.
    [JsonIgnore]
    public bool IsHidden => string.Equals(HiddenFromUI, "True", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsGroupRoot => string.Equals(IsExternalSystemRootArea, "True", StringComparison.OrdinalIgnoreCase);
}

internal sealed record AreaListItem(
    [property: JsonPropertyName("Guid")] Guid Guid,
    [property: JsonPropertyName("Name")] string Name);

internal sealed record AreaListEnvelope<T>([property: JsonPropertyName("Rsp")] AreaListPage<T> Rsp);

internal sealed record AreaListPage<T>(
    [property: JsonPropertyName("Status")] string Status,
    [property: JsonPropertyName("Result")] List<T> Result);
