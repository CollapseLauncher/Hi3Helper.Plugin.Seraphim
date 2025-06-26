using System.Collections.Generic;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System.Linq;
using System.Text.Json;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseSocial
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseSocial>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("social_media_resource_list")]
#endif
    public List<HBRApiResponseSocialResponse>? SocialMediaEntries { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponseSocial? ParseFrom(JsonElement element)
    {
        if (!element.TryGetProperty("social_media_resource_list", out JsonElement arrayElement))
        {
            return null;
        }

        List<HBRApiResponseSocialResponse> entries = [];
        entries.AddRange(arrayElement.EnumerateArray().Select(HBRApiResponseSocialResponse.ParseFrom));

        return new HBRApiResponseSocial
        {
            SocialMediaEntries = entries
        };
    }
#endif
}

public class HBRApiResponseSocialResponse
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseSocialResponse>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("jump_url")]
#endif
    public string? ClickUrl { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("qr_img")]
#endif
    public string? QrImageUrl { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("social_media_channel")]
#endif
    public string? SocialMediaName { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponseSocialResponse ParseFrom(JsonElement element)
    {
        string? clickUrl = element.GetString("jump_url");
        string? qrImageUrl = element.GetString("qr_img");
        string? socialMediaName = element.GetString("social_media_channel");

        return new HBRApiResponseSocialResponse
        {
            ClickUrl = clickUrl,
            QrImageUrl = qrImageUrl,
            SocialMediaName = socialMediaName
        };
    }
#endif
}
