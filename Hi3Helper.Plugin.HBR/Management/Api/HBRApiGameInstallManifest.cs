using Hi3Helper.Plugin.HBR.Utility;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[JsonSerializable(typeof(HBRApiGameInstallManifest))]
public partial class HBRApiGameInstallManifestContext : JsonSerializerContext;

public class HBRApiGameInstallManifest
{
    [JsonPropertyName("source")]
    public string? RootSuffixPath { get; set; }

    [JsonPropertyName("file")]
    public List<GameInstallAsset>? GameAssets { get; set; }
}

public class GameInstallAsset
{
    [JsonConverter(typeof(HBRUlongToBytesJsonConverter))]
    [JsonPropertyName("hash")]
    public byte[]? AssetHash { get; set; }

    [JsonPropertyName("path")]
    public string? AssetPath { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("size")]
    public long AssetSize { get; set; }
}
