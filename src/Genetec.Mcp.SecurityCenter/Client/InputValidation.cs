using System.Globalization;

namespace Genetec.Mcp.SecurityCenter.Client;

public static class InputValidation
{
    public static string Esc(string? value) =>
        value is null ? "" : SecurityCenterRestClient.EscapeQueryValue(value);

    public static string EscPath(string? value) =>
        value is null ? "" : Uri.EscapeDataString(value);

    public static string BuildQuery(params string?[] parts) =>
        string.Join(",", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    public static string? QueryValue(string name, object? value) =>
        value is null ? null : $"{name}={Esc(Convert.ToString(value, CultureInfo.InvariantCulture))}";

    public static string? QueryCollection<T>(string name, IEnumerable<T>? values)
    {
        if (values is null)
            return null;

        var items = values.Select(value => Esc(Convert.ToString(value, CultureInfo.InvariantCulture))).ToArray();
        return items.Length == 0 ? null : $"{name}@{string.Join("@", items)}";
    }

    public static string? QueryTimeRange(string? start, string? end, string member = "TimeRange") =>
        start is not null && end is not null
            ? $"{member}.SetTimeRange({Esc(start)},{Esc(end)})"
            : null;
}