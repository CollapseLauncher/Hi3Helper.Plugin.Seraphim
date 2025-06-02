using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseMedia>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseSocial>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseGameConfig>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseGameConfigRef>))]
public partial class HBRApiResponseContext : JsonSerializerContext;

public class HBRApiResponse<T>
{
    [JsonPropertyName("code")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ReturnCode { get; set; }

    [JsonPropertyName("data")]
    public T? ResponseData { get; set; }

    [JsonPropertyName("msg")]
    public string? ReturnMessage { get; set; }

    public void EnsureSuccessCode()
    {
        if (ResponseData == null || ReturnCode != 200) 
        {
            throw new HttpRequestException($"API returned unsuccessful code: {ReturnCode} ({ReturnMessage})", null, (HttpStatusCode)ReturnCode);
        }
    }
}
