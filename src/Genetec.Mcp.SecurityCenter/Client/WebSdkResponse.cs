using System.Text.Json;
using System.Text.Json.Nodes;

namespace Genetec.Mcp.SecurityCenter.Client;

internal static class WebSdkResponse
{
    public static string Unwrap(string responseBody, int? pageSize = null, bool includeEnvelope = false)
    {
        if (pageSize is { } size) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, nameof(pageSize));
        using var document = JsonDocument.Parse(responseBody);
        var result = document.RootElement;
        var hasEnvelope = false;
        var preserveEnvelope = false;
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("Rsp", out var rsp))
        {
            hasEnvelope = true;
            var status = rsp.GetProperty("Status").GetString();
            if (status == "Fail")
                throw new InvalidOperationException($"Security Center error: {(rsp.TryGetProperty("Result", out var failure) ? failure.GetRawText() : "Unknown error")}");

            preserveEnvelope = includeEnvelope || status is "TooManyResults" or "Partial";
            if (rsp.TryGetProperty("Result", out var payload))
                result = payload;
            else
                return preserveEnvelope ? responseBody : JsonSerializer.Serialize(new { status });
        }

        // The Web SDK can include a look-ahead row. Limit only the report's row
        // collection; retain its columns and all upstream status/metadata.
        var rows = result.ValueKind == JsonValueKind.Array ? result
            : result.ValueKind == JsonValueKind.Object && result.TryGetProperty("Body", out var body) && body.ValueKind == JsonValueKind.Array
                ? body : default;
        if (pageSize is not { } count || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() <= count)
            return preserveEnvelope || !hasEnvelope ? responseBody : result.GetRawText();

        var output = JsonNode.Parse(preserveEnvelope ? responseBody : result.GetRawText())!;
        var payloadNode = preserveEnvelope ? output["Rsp"]!["Result"] : output;
        var rowNodes = payloadNode as JsonArray ?? (JsonArray)payloadNode!["Body"]!;
        while (rowNodes.Count > count)
            rowNodes.RemoveAt(rowNodes.Count - 1);
        return output.ToJsonString();
    }

    public static T Deserialize<T>(string response, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(response);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("Rsp", out var rsp) &&
            rsp.TryGetProperty("Status", out var status) &&
            status.GetString() is "TooManyResults" or "Partial")
            throw new InvalidOperationException($"Security Center returned an incomplete {status.GetString()} response for {typeof(T).Name}.");

        return JsonSerializer.Deserialize<T>(response, options)
            ?? throw new InvalidOperationException($"Response could not be deserialized to {typeof(T).Name}.");
    }
}
