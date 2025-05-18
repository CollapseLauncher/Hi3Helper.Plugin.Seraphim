using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Utility;

internal static class HBRUtility
{
    internal static string GetApiAuthToken(string salt1, string salt2, string? gameTag = null)
    {
        ArgumentNullException.ThrowIfNull(salt1, nameof(salt1));
        ArgumentNullException.ThrowIfNull(salt2, nameof(salt2));

        HBRLauncherAuthTokenHeader headerResponse     = HBRLauncherAuthTokenHeader.CreateFromCurrent(gameTag);
        string                     headerResponseJson = JsonSerializer.Serialize(headerResponse, HBRLauncherAuthTokenContext.Default.HBRLauncherAuthTokenHeader);

        Span<byte> headerResponseJsonUtf8 = stackalloc byte[headerResponseJson.Length + salt1.Length + salt2.Length];
        Span<byte> signSaltChecksum       = stackalloc byte[16];

        int offset = 0;
        _ = Encoding.UTF8.TryGetBytes(headerResponseJson, headerResponseJsonUtf8, out int written1);
        offset += written1;
        _ = Encoding.UTF8.TryGetBytes(salt1, headerResponseJsonUtf8[offset..], out int written2);
        offset += written2;
        _ = Encoding.UTF8.TryGetBytes(salt2, headerResponseJsonUtf8[offset..], out int _);

        _ = MD5.HashData(headerResponseJsonUtf8, signSaltChecksum);

        HBRLauncherAuthToken tokenResponse = new()
        {
            Header = headerResponse,
            Sign   = Convert.ToHexStringLower(signSaltChecksum)
        };

        return JsonSerializer.Serialize(tokenResponse, HBRLauncherAuthTokenContext.Default.HBRLauncherAuthToken);
    }

    internal static HttpClient CreateApiHttpClient(string? gameTag = null, bool isUseAuthToken = true, bool useCompression = true, string? authSalt1 = "", string? authSalt2 = "")
        => CreateApiHttpClientBuilder(gameTag, isUseAuthToken, useCompression, authSalt1, authSalt2).Create();

    internal static PluginHttpClientBuilder<SocketsHttpHandler> CreateApiHttpClientBuilder(string? gameTag = null, bool isUseAuthToken = true, bool useCompression = true, string? authSalt1 = "", string? authSalt2 = "")
    {
        PluginHttpClientBuilder<SocketsHttpHandler> builder = new PluginHttpClientBuilder()
            .SetUserAgent("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) HBR_EN_Gamelauncher/1.4.1 Chrome/108.0.5359.62 Electron/22.0.0 Safari/537.36");

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (isUseAuthToken)
        {
            if (authSalt1 == null)
            {
                throw new ArgumentNullException(nameof(authSalt1), "authSalt1 cannot be empty when isUseAuthToken is set! Use string.Empty if you want to ignore it instead");
            }

            if (authSalt2 == null)
            {
                throw new ArgumentNullException(nameof(authSalt2), "authSalt2 cannot be empty when isUseAuthToken is set! Use string.Empty if you want to ignore it instead");
            }

            string currentAuthToken = GetApiAuthToken(authSalt1, authSalt2, gameTag);
            Console.WriteLine(currentAuthToken);
            builder.AddHeader("Authorization", currentAuthToken);
        }

        if (!useCompression)
        {
            builder.SetAllowedDecompression(DecompressionMethods.None);
        }

        builder.AddHeader("sec-ch-ua", "\"Not?A_Brand\";v=\"8\", \"Chromium\";v=\"108\"")
            .AddHeader("sec-ch-ua-mobile", "?0")
            .AddHeader("sec-ch-ua-platform", "Windows")
            .AddHeader("Sec-Fetch-Site", "cross-site")
            .AddHeader("Sec-Fetch-Mode", "cors")
            .AddHeader("Sec-Fetch-Dest", "empty");

        return builder;
    }
}
