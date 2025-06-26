using System.Collections.Generic;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(HBRApiGameInstallManifest))]
public partial class HBRApiGameInstallManifestContext : JsonSerializerContext;
#endif

public class HBRApiGameInstallManifest
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiGameInstallManifest>,
      IJsonStreamParsable<HBRApiGameInstallManifest>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("source")]
#endif
    public string? RootSuffixPath { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("file")]
#endif
    public List<GameInstallAsset>? GameAssets { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiGameInstallManifest ParseFrom(Stream stream, bool isDisposeStream = false,
        JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<HBRApiGameInstallManifest> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
        CancellationToken token = default)
    {
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(stream, options, token).ConfigureAwait(false);
            return await Task.Factory.StartNew(() => ParseFrom(document.RootElement), token);
        }
        finally
        {
            if (isDisposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public static HBRApiGameInstallManifest ParseFrom(JsonElement rootElement)
    {
        string? rootSuffixPath = rootElement.GetString("source");
        if (!rootElement.TryGetProperty("file", out JsonElement assetsArray))
        {
            throw new JsonException("Property: \"file\" is not defined");
        }

        List<GameInstallAsset> assetList = [];
        foreach (JsonElement asset in assetsArray.EnumerateArray())
        {
            string hashAsStringUlong = asset.GetStringNonNullOrEmpty("hash");
            string path = asset.GetStringNonNullOrEmpty("path");
            string sizeAsStringLong = asset.GetStringNonNullOrEmpty("size");

            byte[] hash = new byte[sizeof(ulong)];

            if (!ulong.TryParse(hashAsStringUlong, out ulong hashAsUlong) ||
                !BitConverter.TryWriteBytes(hash, hashAsUlong))
            {
                throw new JsonException("Cannot parse file.hash property!");
            }

            if (!long.TryParse(sizeAsStringLong, out long size))
            {
                throw new JsonException("Cannot parse file.size property!");
            }

            assetList.Add(new GameInstallAsset
            {
                AssetPath = path,
                AssetHash = hash,
                AssetSize = size
            });
        }

        return new HBRApiGameInstallManifest
        {
            RootSuffixPath = rootSuffixPath,
            GameAssets = assetList
        };
    }
#endif
}

public class GameInstallAsset
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("hash")]
    [JsonConverter(typeof(Utf8SpanParsableToBytesJsonConverter<ulong>))]
#endif
    public byte[]? AssetHash { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("path")]
#endif
    public string? AssetPath { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("size")]
#endif
    public long AssetSize { get; set; }
}
