using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Tools;

public sealed record BatchCreateResult(
    string Mode,
    int RequestedCount,
    int SuccessCount,
    int FailureCount,
    List<BatchCreateResultItem> Results);

public sealed record BatchCreateResultItem(
    int Index,
    bool Success,
    Guid? Guid = null,
    string? Error = null,
    string? Warning = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Response = null);
