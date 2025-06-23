using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

[JsonSerializable(typeof(HBRGameManifest))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentSize = 2,
    IndentCharacter = ' ',
    NewLine = "\n")]
public partial class HBRGameLauncherConfigContext : JsonSerializerContext;

public class HBRGameLauncherConfig
{
    internal static string GetConfigSalt(params ReadOnlySpan<string?> salts)
    {
        string configSalt = string.Join(';', salts);

        Span<byte> configSaltInBytes = stackalloc byte[Encoding.UTF8.GetByteCount(configSalt)];
        _ = Encoding.UTF8.GetBytes(configSalt, configSaltInBytes);

        Span<byte> hashData = stackalloc byte[MD5.HashSizeInBytes];
        MD5.HashData(configSaltInBytes, hashData);

        return Convert.ToBase64String(hashData);
    }
}

public class HBRGameManifest
{
    [JsonPropertyName("basis")]
    public string? GamePackageBasis { get; set; }

    [JsonPropertyName("name")]
    public string? GameTag { get; set; }

    [JsonPropertyName("version")]
    public string? GameVersion { get; set; }

    [JsonPropertyName("source")]
    public string? ParentDir { get; set; }

    [JsonPropertyName("files")]
    public List<HBRGameManifestEntry>? ManifestEntries { get; set; }

    [JsonPropertyName("vc")]
    public string ConfigSignature => HBRGameLauncherConfig.GetConfigSalt(GameTag, $"{GameVersion}", GamePackageBasis);
}

public class HBRGameManifestEntry
{
    [JsonPropertyName("hash")]
    [JsonConverter(typeof(HBRCrc64HashConverter))]
    public byte[]? AssetCrc64Hash { get; set; }

    [JsonIgnore]
    public string AssetCrc64HashAsString => $"{BitConverter.ToUInt64(AssetCrc64Hash)}";

    [JsonPropertyName("path")]
    public string? AssetPath { get; set; }

    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long AssetSize { get; set; }

    [JsonPropertyName("vc")]
    public string ConfigSignature => HBRGameLauncherConfig.GetConfigSalt(AssetPath, AssetCrc64HashAsString, $"{AssetSize}");
}

public class HBRCrc64HashConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !ulong.TryParse(reader.ValueSpan, null, out ulong hash))
        {
            return null;
        }

        return BitConverter.GetBytes(hash);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        ulong byteAsUlong = BitConverter.ToUInt64(value);
        writer.WriteStringValue($"{byteAsUlong}");
    }
}