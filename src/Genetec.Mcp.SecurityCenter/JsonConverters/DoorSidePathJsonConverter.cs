using System.Text.Json;
using System.Text.Json.Serialization;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.JsonConverters;

internal sealed class DoorSidePathJsonConverter : JsonConverter<DoorSidePath>
{
    public override DoorSidePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "Alpha" => DoorSidePath.DoorSideIn,
            "Omega" => DoorSidePath.DoorSideOut,
            _ => throw new JsonException("AccessPointSide must be Alpha or Omega for a door side.")
        };

    public override void Write(Utf8JsonWriter writer, DoorSidePath value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
