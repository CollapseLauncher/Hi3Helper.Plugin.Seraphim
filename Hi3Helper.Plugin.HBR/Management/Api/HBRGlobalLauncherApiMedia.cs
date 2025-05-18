using System;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Hi3Helper.Plugin.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.IO;
using Hi3Helper.Plugin.Core.Utility;

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiMedia : LauncherApiMediaBase, ILauncherApiMedia
{
    protected override HttpClient ApiResponseHttpClient { get; }
    protected          HttpClient ApiDownloadHttpClient { get; }

    private HBRApiResponse<HBRApiResponseMedia>? ApiResponse { get; set; }
    private string ApiBaseUrl { get; }

    internal HBRGlobalLauncherApiMedia(string apiBaseUrl, string gameTag, string authSalt1, string authSalt2)
    {
        ApiBaseUrl            = apiBaseUrl;
        ApiResponseHttpClient = HBRUtility.CreateApiHttpClient(gameTag, true, true, authSalt1, authSalt2);
        ApiDownloadHttpClient = HBRUtility.CreateApiHttpClient(gameTag, false, false);
    }

    public override unsafe nint GetBackgroundEntries()
    {
        if (ApiResponse?.ResponseData == null)
        {
            return nint.Zero;
        }

        LauncherPathEntry* entry    = (LauncherPathEntry*)NativeMemory.AllocZeroed((nuint)sizeof(LauncherPathEntry));
        Span<char>         pathSpan = new(entry->Path, LauncherPathEntry.PathMaxLength - 1);

        ReadOnlySpan<char> urlSpan     = ApiResponse.ResponseData.BackgroundImageUrl.AsSpan();
        ulong              fileHashCrc = ApiResponse.ResponseData.BackgroundImageChecksum;

        entry->FileHashLength = sizeof(ulong);
        entry->NextEntry      = nint.Zero;

        urlSpan.CopyTo(pathSpan[..^1]);
        MemoryMarshal.Write(new Span<byte>(entry->FileHash, entry->FileHashLength), in fileHashCrc);

        return (nint)entry;
    }

    public override nint GetLogoOverlayEntries() => nint.Zero;

    public override LauncherBackgroundFlag GetBackgroundFlag()
        => LauncherBackgroundFlag.IsSourceFile | LauncherBackgroundFlag.TypeIsImage;

    public override LauncherBackgroundFlag GetLogoFlag()
        => LauncherBackgroundFlag.None;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        using HttpResponseMessage message = await ApiResponseHttpClient.GetAsync(ApiBaseUrl + "api/launcher/base/config", HttpCompletionOption.ResponseHeadersRead, token);
        message.EnsureSuccessStatusCode();

        string jsonResponse = await message.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger?.LogTrace("API Media response: {JsonResponse}", jsonResponse);

        ApiResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseMedia>>(jsonResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseMedia);
        ApiResponse!.EnsureSuccessCode();

        return 0;
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, byte[] fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
    }
}
