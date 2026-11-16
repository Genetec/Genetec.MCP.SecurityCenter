using System.Text.Json.Serialization;

namespace Genetec.Mcp.SecurityCenter.Client;

internal enum DeviceType
{
    Reader = 1,
    Input = 2,
    Output = 3,
    Lpr = 4,
    VideoEncoder = 5,
    Serial = 6,
    AlarmPanelInput = 7,
    AlarmPanelOutput = 8,
    AudioDecoder = 9,
    AudioEncoder = 10,
    VideoUnitInput = 11,
    VideoUnitOutput = 12,
    AnalogMonitor = 13
}

internal sealed record DeviceMetadataResponse : EntityMetadataResponse
{
    [JsonConverter(typeof(JsonStringEnumConverter<DeviceType>))]
    public DeviceType? DeviceType { get; init; }
}
