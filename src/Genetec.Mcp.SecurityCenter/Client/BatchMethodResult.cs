using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Client;

public sealed record BatchMethodResult(
    string Mode,
    int RequestedCount,
    int SuccessCount,
    int FailureCount,
    List<BatchMethodResultItem> Results,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Method = null);

public sealed record BatchMethodResultItem(
    int Index,
    bool Success,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? Id = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Arg = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Response = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error = null);
