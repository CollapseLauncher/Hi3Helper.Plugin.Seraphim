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
    private static PluginDisposableMemoryMarshal backgroundEntriesMarshal = PluginDisposableMemoryMarshal.Empty;

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

    public override bool GetBackgroundEntries(out nint handle, out int count, out bool isDisposable)
    {
        using (ThisInstanceLock.EnterScope())
        {
            try
            {
                if (backgroundEntriesMarshal.Length != 0)
                {
                    return true;
                }

                PluginDisposableMemory<LauncherPathEntry> memory = PluginDisposableMemory<LauncherPathEntry>.Alloc();

                ref LauncherPathEntry entry = ref memory[0];
                entry.InitInner();

                if (ApiResponse?.ResponseData == null)
                {
                    isDisposable = false;
                    handle = nint.Zero;
                    count = 0;
                    return false;
                }

                ReadOnlySpan<char> urlSpan = ApiResponse.ResponseData.BackgroundImageUrl.AsSpan();
                ulong fileHashCrc = ApiResponse.ResponseData.BackgroundImageChecksum;

                entry.FileHashLength = sizeof(ulong);
                urlSpan.CopyToUtf8(entry.Path.AsSpan()[..^1]);
                MemoryMarshal.Write(entry.FileHash.AsSpan(), in fileHashCrc);

                backgroundEntriesMarshal = memory.ToUnmanagedMarshal();
                return true;
            }
            finally
            {
                isDisposable = backgroundEntriesMarshal.IsDisposable;
                handle = backgroundEntriesMarshal.Handle;
                count = backgroundEntriesMarshal.Length;
            }
        }
    }

    public override bool GetLogoOverlayEntries(out nint handle, out int count, out bool isDisposable)
    {
        isDisposable = false;
        handle = nint.Zero;
        count = 0;
        return false;
    }

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

    public override void Dispose()
    {
        base.Dispose();
        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient.Dispose();

            backgroundEntriesMarshal.ToManagedSpan<LauncherPathEntry>().Dispose();
            backgroundEntriesMarshal = PluginDisposableMemoryMarshal.Empty;
            ApiResponse = null;
        }
    }
}
