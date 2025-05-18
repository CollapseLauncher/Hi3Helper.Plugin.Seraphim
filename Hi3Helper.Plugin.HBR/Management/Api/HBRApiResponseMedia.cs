using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseMedia
{
    [JsonPropertyName("launcher_background_img")]
    public string? BackgroundImageUrl { get; set; }

    [JsonPropertyName("launcher_background_img_crc64")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong BackgroundImageChecksum { get; set; }
}
