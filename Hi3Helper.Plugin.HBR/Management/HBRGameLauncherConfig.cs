using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

[JsonSerializable(typeof(HBRGameLauncherConfig))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class HBRGameLauncherConfigContext : JsonSerializerContext;

public class HBRGameLauncherConfig
{
    [JsonPropertyName("tag")]
    public string? GameTag { get; set; }

    [JsonPropertyName("name")]
    public string? ExecutableName { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("vc")]
    public string ConfigSignature
    {
        get
        {
            string configSalt = $"{ExecutableName};{GameTag};{Version}";

            Span<byte> configSaltInBytes = stackalloc byte[Encoding.UTF8.GetByteCount(configSalt)];
            _ = Encoding.UTF8.GetBytes(configSalt, configSaltInBytes);

            Span<byte> hashData = stackalloc byte[MD5.HashSizeInBytes];
            MD5.HashData(configSaltInBytes, hashData);

            return Convert.ToBase64String(hashData);
        }
    }
}
