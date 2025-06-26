#if !USELIGHTWEIGHTJSONPARSER
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;
#else
using System;
using System.Text.Json;
using Hi3Helper.Plugin.Core.Utility.Json;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseMedia
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseMedia>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("launcher_background_img")]
#endif
    public string? BackgroundImageUrl { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("launcher_background_img_crc64")]
    [JsonConverter(typeof(Utf8SpanParsableToBytesJsonConverter<ulong>))]
#endif
    public byte[]? BackgroundImageChecksum { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponseMedia ParseFrom(JsonElement element)
    {
        HBRApiResponseMedia returnValue = new HBRApiResponseMedia
        {
            BackgroundImageUrl = element.GetString("launcher_background_img")
        };

        if (element.GetString("launcher_background_img_crc64") is { } uint64Str &&
            ulong.TryParse(uint64Str, out ulong uint64))
        {
            returnValue.BackgroundImageChecksum = BitConverter.GetBytes(uint64);
        }

        return returnValue;
    }
#endif
}
