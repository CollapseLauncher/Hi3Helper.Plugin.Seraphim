using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Utility;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Utility;

internal static class HBRCDNUtility
{
    private const string ExCdnFileSuffix = "inhouse-plugin/heavenburnsred/";

    private const string ExCdn1Url = "https://r2.bagelnl.my.id/cl-cdn/" + ExCdnFileSuffix;
    private const string ExCdn2Url = "https://cnb.cool/CollapseLauncher/ReleaseRepo/-/git/raw/main/" + ExCdnFileSuffix;
    private const string ExCdn3Url = "https://cdn.collapselauncher.com/cl-cdn/" + ExCdnFileSuffix;
    private const string ExCdn4Url = "https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/raw/main/" + ExCdnFileSuffix;
    private const string ExCdn5Url = "https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/" + ExCdnFileSuffix;

    internal static readonly string[] BaseCdnUrl = [ExCdn1Url, ExCdn2Url, ExCdn3Url, ExCdn4Url, ExCdn5Url];

    internal static Task<HttpResponseMessage?> GetResponseFromUrlOrCdnAsync(
        this HttpClient   client,
        Uri               url,
        HttpMethod?       method = null,
        CancellationToken token  = default)
        => client.GetResponseFromUrlOrCdnAsync(url.ToString(),
                                               method,
                                               token);

    internal static async Task<HttpResponseMessage?> GetResponseFromUrlOrCdnAsync(
        this HttpClient   client,
        string            url,
        HttpMethod?       method = null,
        CancellationToken token  = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        method ??= HttpMethod.Get;
        HttpResponseMessage? responseFromSingle = null;

        // Try to get response from single URL first.
        if (url.StartsWith("http") &&
            Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri))
        {
            responseFromSingle = await GetResponseFromSingleUrlAsync(client, parsedUri, method, token);
        }

        if (responseFromSingle != null)
            return responseFromSingle;

        // Randomize CDN and get the full CDN request.
        foreach (string cdnBaseUri in Random.Shared.GetItems(BaseCdnUrl, BaseCdnUrl.Length))
        {
            string fullUrl = cdnBaseUri.CombineUrlFromString(url.TrimStart('/'));
            responseFromSingle ??= await GetResponseFromSingleUrlAsync(client, new Uri(fullUrl), method, token);
            if (responseFromSingle != null)
                return responseFromSingle;
        }

        return null;

        static async Task<HttpResponseMessage?> GetResponseFromSingleUrlAsync(
            HttpClient        client,
            Uri               url,
            HttpMethod        method,
            CancellationToken token)
        {
            HttpResponseMessage? response = null;
            HttpRequestMessage?  request  = null;
            bool                 isFail   = true;

            try
            {
                request  = new HttpRequestMessage(method, url);
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                    return null;

                isFail = false;
                return response;
            }
            finally
            {
                if (isFail)
                {
                    response?.Dispose();
                    request?.Dispose();
                }
            }
        }
    }
}
