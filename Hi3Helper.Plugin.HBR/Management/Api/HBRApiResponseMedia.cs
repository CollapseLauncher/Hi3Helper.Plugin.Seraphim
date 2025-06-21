using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseMedia
{
    [JsonPropertyName("launcher_background_img")]
    public string? BackgroundImageUrl { get; set; }

    [JsonPropertyName("launcher_background_img_crc64")]
    [JsonConverter(typeof(Utf8SpanParsableToBytesJsonConverter<ulong>))]
    public byte[]? BackgroundImageChecksum { get; set; }
}
