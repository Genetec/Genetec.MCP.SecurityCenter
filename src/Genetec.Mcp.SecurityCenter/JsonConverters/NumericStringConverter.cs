using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Genetec.Mcp.SecurityCenter.JsonConverters;

/// <summary>
/// Allows string tool parameters to accept JSON number tokens.
/// Needed because MCP clients without full schema type info may infer that
/// digit-only values (e.g. "1234") should be sent as JSON numbers rather than strings.
/// </summary>
internal sealed class NumericStringConverter : JsonConverter<string>
{
    public static readonly JsonSerializerOptions ToolOptions = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            var hasValidationAttributes = false;
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is null) continue;

                if (property.AttributeProvider.IsDefined(typeof(ValidationAttribute), inherit: true))
                    hasValidationAttributes = true;

                // DataAnnotations validation cannot distinguish an omitted non-nullable
                // value type from its default value. Make [Required] properties required
                // during JSON deserialization so missing enums, GUIDs, and booleans fail.
                if (property.AttributeProvider.IsDefined(typeof(RequiredAttribute), inherit: true))
                    property.IsRequired = true;
            }

            if (!hasValidationAttributes) return;
            typeInfo.OnDeserialized = obj =>
                Validator.ValidateObject(obj, new ValidationContext(obj), validateAllProperties: true);
        });

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            TypeInfoResolver = resolver,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        //options.Converters.Add(new NumericStringConverter());
        return options;
    }

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
            _ => reader.GetString()
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
