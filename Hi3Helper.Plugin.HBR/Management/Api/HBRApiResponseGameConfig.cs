using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseGameConfig
{
    [JsonPropertyName("game_lowest_version")]
    public string? PreviousVersion { get; set; }

    [JsonPropertyName("game_latest_version")]
    public string? CurrentVersion { get; set; }

    [JsonPropertyName("game_latest_file_path")]
    public string? GameZipLocalPath { get; set; }

    [JsonPropertyName("game_start_exe_name")]
    public string? GameExecutableFileName { get; set; }

    [JsonPropertyName("file_url")]
    public string? ZipFileUrl { get; set; }

    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong ZipFileSize { get; set; }
}
