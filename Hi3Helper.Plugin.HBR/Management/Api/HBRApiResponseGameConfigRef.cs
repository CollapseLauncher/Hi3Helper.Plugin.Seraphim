using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

internal class HBRApiResponseGameConfigRef
{
    [JsonPropertyName("url")]
    public string? DownloadAssetsReferenceUrl { get; set; }
}
