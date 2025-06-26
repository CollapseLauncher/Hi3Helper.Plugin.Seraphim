using System;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System.Buffers;
using System.Text;
using System.Text.Json;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(HBRLauncherAuthToken))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class HBRLauncherAuthTokenContext : JsonSerializerContext;
#endif

public class HBRLauncherAuthToken
#if USELIGHTWEIGHTJSONPARSER
    : IJsonStringSerializable<HBRLauncherAuthToken>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("head")]
#endif
    public HBRLauncherAuthTokenHeader? Header { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("sign")]
#endif
    public string? Sign { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static string SerializeToString(HBRLauncherAuthToken obj, JsonWriterOptions options = default)
    {
        ArrayBufferWriter<byte> arrayBufferWriter = new(4 << 10);

        using Utf8JsonWriter writer = new Utf8JsonWriter(arrayBufferWriter, options);
        writer.WriteStartObject();

        if (obj.Header != null)
        {
            writer.WriteStartObject("head");

            if (obj.Header.GameTag != null)
                writer.WriteString("game_tag"u8, obj.Header.GameTag);

            if (obj.Header.TimeUtc != 0)
                writer.WriteNumber("time"u8, obj.Header.TimeUtc);

            writer.WriteEndObject();
        }

        if (obj.Sign != null)
            writer.WriteString("sign"u8, obj.Sign);

        writer.WriteEndObject();
        writer.Flush();

        ReadOnlySpan<byte> bufferWritten = arrayBufferWriter.WrittenSpan;
        string writtenRet = Encoding.UTF8.GetString(bufferWritten);

        arrayBufferWriter.Clear();
        return writtenRet;
    }
#endif
}

public class HBRLauncherAuthTokenHeader
#if USELIGHTWEIGHTJSONPARSER
    : IJsonStringSerializable<HBRLauncherAuthTokenHeader>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("game_tag")]
#endif
    public string? GameTag { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("time")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
#endif
    public long TimeUtc { get; set; }

    public static HBRLauncherAuthTokenHeader CreateFromCurrent(string? gameTag = null)
        => new()
        {
            GameTag = gameTag ?? "HBR_EN",
            TimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

#if USELIGHTWEIGHTJSONPARSER
    public static string SerializeToString(HBRLauncherAuthTokenHeader obj, JsonWriterOptions options = default)
    {
        ArrayBufferWriter<byte> arrayBufferWriter = new(4 << 10);

        using Utf8JsonWriter writer = new Utf8JsonWriter(arrayBufferWriter, options);
        writer.WriteStartObject();

        if (obj.GameTag != null)
            writer.WriteString("game_tag"u8, obj.GameTag);

        if (obj.TimeUtc != 0)
            writer.WriteNumber("time"u8, obj.TimeUtc);

        writer.WriteEndObject();
        writer.Flush();

        ReadOnlySpan<byte> bufferWritten = arrayBufferWriter.WrittenSpan;
        string writtenRet = Encoding.UTF8.GetString(bufferWritten);

        arrayBufferWriter.Clear();
        return writtenRet;
    }
#endif
}
