using System;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[JsonSerializable(typeof(HBRLauncherAuthToken))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class HBRLauncherAuthTokenContext : JsonSerializerContext;

public class HBRLauncherAuthToken
{
    [JsonPropertyName("head")]
    public HBRLauncherAuthTokenHeader? Header { get; set; }

    [JsonPropertyName("sign")]
    public string? Sign { get; set; }
}

public class HBRLauncherAuthTokenHeader
{
    [JsonPropertyName("game_tag")]
    public string? GameTag { get; set; }

    [JsonPropertyName("time")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long TimeUtc { get; set; }

    public static HBRLauncherAuthTokenHeader CreateFromCurrent(string? gameTag = null)
        => new()
        {
            GameTag = gameTag ?? "HBR_EN",
            TimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
}
