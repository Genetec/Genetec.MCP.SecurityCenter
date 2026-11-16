using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Genetec.Mcp.SecurityCenter.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public NotEmptyAttribute() : base("The {0} field must not be empty.") { }

    public override bool IsValid(object? value) => value switch
    {
        Guid guid => guid != Guid.Empty,
        string text => !string.IsNullOrWhiteSpace(text),
        _ => false
    };
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed partial class Iso8601Attribute : ValidationAttribute
{
    public Iso8601Attribute() : base("The {0} field must be a valid ISO 8601 date/time.") { }
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")]
    private static partial Regex DatePrefix();
    public override bool IsValid(object? value) =>
        value is null || (value is string s && DatePrefix().IsMatch(s) && DateTimeOffset.TryParse(s, out _));
}
