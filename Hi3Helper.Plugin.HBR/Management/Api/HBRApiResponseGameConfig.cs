using Hi3Helper.Plugin.Core.Management;

#if !USELIGHTWEIGHTJSONPARSER
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;
#else
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Utility.Json;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseGameConfig
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseGameConfig>,
      IJsonStreamParsable<HBRApiResponseGameConfig>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("game_lowest_version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
#endif
    public GameVersion PreviousVersion { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("game_latest_version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
#endif
    public GameVersion CurrentVersion { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("game_latest_file_path")]
#endif
    public string? GameZipLocalPath { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("game_start_exe_name")]
#endif
    public string? GameExecutableFileName { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("file_url")]
#endif
    public string? ZipFileUrl { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
#endif
    public ulong ZipFileSize { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponseGameConfig ParseFrom(Stream stream, bool isDisposeStream = false,
        JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<HBRApiResponseGameConfig> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
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

    public static HBRApiResponseGameConfig ParseFrom(JsonElement element)
        => new()
        {
            PreviousVersion = element.GetValue<GameVersion>("game_lowest_version"),
            CurrentVersion = element.GetValue<GameVersion>("game_latest_version"),
            GameZipLocalPath = element.GetString("game_latest_file_path"),
            GameExecutableFileName = element.GetString("game_start_exe_name"),
            ZipFileUrl = element.GetString("file_url"),
            ZipFileSize = element.GetValue<ulong>("size")
        };
#endif
}
