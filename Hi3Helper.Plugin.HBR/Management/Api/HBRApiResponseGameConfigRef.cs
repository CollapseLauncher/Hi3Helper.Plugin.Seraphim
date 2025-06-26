#if !USELIGHTWEIGHTJSONPARSER
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

public class HBRApiResponseGameConfigRef
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseGameConfigRef>,
      IJsonStreamParsable<HBRApiResponseGameConfigRef>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("url")]
#endif
    public string? DownloadAssetsReferenceUrl { get; set; }

#if USELIGHTWEIGHTJSONPARSER
    public static HBRApiResponseGameConfigRef ParseFrom(Stream stream, bool isDisposeStream = false,
        JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<HBRApiResponseGameConfigRef> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
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

    public static HBRApiResponseGameConfigRef ParseFrom(JsonElement element)
        => new()
        {
            DownloadAssetsReferenceUrl = element.GetString("url")
        };
#endif
}
