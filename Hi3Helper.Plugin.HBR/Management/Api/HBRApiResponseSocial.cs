using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseSocial
{
    [JsonPropertyName("social_media_resource_list")]
    public List<HBRApiResponseSocialResponse>? SocialMediaEntries { get; set; }
}

public class HBRApiResponseSocialResponse
{
    [JsonPropertyName("jump_url")]
    public string? ClickUrl { get; set; }

    [JsonPropertyName("qr_img")]
    public string? QrImageUrl { get; set; }

    [JsonPropertyName("social_media_channel")]
    public string? SocialMediaName { get; set; }
}
