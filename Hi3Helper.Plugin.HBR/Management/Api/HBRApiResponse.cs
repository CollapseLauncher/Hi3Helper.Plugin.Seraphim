using System.Net;
using System.Net.Http;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseMedia>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseSocial>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseGameConfig>))]
[JsonSerializable(typeof(HBRApiResponse<HBRApiResponseGameConfigRef>))]
public partial class HBRApiResponseContext : JsonSerializerContext;
#endif

public class HBRApiResponse<T>
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponse<T>>,
      IJsonStreamParsable<HBRApiResponse<T>>
    where T : IJsonElementParsable<T>, new()
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("code")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
#endif
    public int ReturnCode { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("data")]
#endif
    public T? ResponseData { get; set; }

#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("msg")]
#endif
    public string? ReturnMessage { get; set; }

    public void EnsureSuccessCode()
    {
        if (ResponseData == null || ReturnCode != 200) 
        {
            throw new HttpRequestException($"API returned unsuccessful code: {ReturnCode} ({ReturnMessage})", null, (HttpStatusCode)ReturnCode);
        }
    }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponse<T> ParseFrom(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<HBRApiResponse<T>> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
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

    public static HBRApiResponse<T> ParseFrom(JsonElement rootElement)
    {
        string? returnMessage = rootElement.GetString("msg");
        int returnCode = rootElement.GetValue<int>("code");

        T? innerValue = default;
        if (rootElement.TryGetProperty("data", out JsonElement dataElement))
        {
            innerValue = T.ParseFrom(dataElement);
        }

        return new HBRApiResponse<T>
        {
            ResponseData  = innerValue,
            ReturnCode    = returnCode,
            ReturnMessage = returnMessage
        };
    }
#endif
}
