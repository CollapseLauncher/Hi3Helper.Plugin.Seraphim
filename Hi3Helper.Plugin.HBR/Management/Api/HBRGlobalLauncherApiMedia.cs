using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiMedia(string apiResponseBaseUrl, string gameTag, string authSalt1, string authSalt2) : LauncherApiMediaBase
{
    [field: AllowNull, MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(gameTag, true, true, authSalt1, authSalt2);
        set;
    }

    [field: AllowNull, MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(gameTag, false, false);
        set;
    }

    private static TimeSpan _refreshTokenTime = TimeSpan.FromMinutes(30);
    private DateTimeOffset _nextRefreshToken;

    protected override string ApiResponseBaseUrl { get; } = apiResponseBaseUrl;

    private HBRApiResponse<HBRApiResponseMedia>? ApiResponse { get; set; }

    private List<HBRApiResponseMediaOverrideEntry>? ApiOverrideEntriesResponse { get; set; }

    public override unsafe void GetBackgroundEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        using (ThisInstanceLock.EnterScope())
        {
            List<HBRApiResponseMediaOverrideEntry> validOverrideEntries = [];
            if (ApiOverrideEntriesResponse != null)
                validOverrideEntries.AddRange(ApiOverrideEntriesResponse.Where(x => x.ExpireDate != default && DateTimeOffset.Now < x.ExpireDate));

            int entryCount = validOverrideEntries is { Count: > 0 } ? validOverrideEntries.Count : 0;
            if (ApiResponse?.ResponseData != null)
                entryCount++;

            PluginDisposableMemory<LauncherPathEntry> backgroundEntries = entryCount > 0
                ? PluginDisposableMemory<LauncherPathEntry>.Alloc(entryCount)
                : PluginDisposableMemory<LauncherPathEntry>.Empty;

            try
            {
                if (entryCount == 0)
                    return;

                int offset = 0;
                foreach (HBRApiResponseMediaOverrideEntry overrideEntry in validOverrideEntries)
                {
                    ref LauncherPathEntry overrideEntryRef = ref backgroundEntries[offset++];
                    overrideEntryRef.Write(overrideEntry.BackgroundUrl, Span<byte>.Empty);
                }

                if (ApiResponse?.ResponseData == null)
                {
                    return;
                }

                ref LauncherPathEntry entry = ref backgroundEntries[offset];

                byte[]? fileHashCrc = ApiResponse.ResponseData.BackgroundImageChecksum;
                void* ptr = fileHashCrc == null ? null : (void*)Marshal.UnsafeAddrOfPinnedArrayElement(fileHashCrc, 0);

                entry.Write(ApiResponse.ResponseData.BackgroundImageUrl, ptr == null ? Span<byte>.Empty : new Span<byte>(ptr, sizeof(ulong)));
            }
            finally
            {
                isDisposable = backgroundEntries.IsDisposable == 1;
                isAllocated  = entryCount > 0;
                handle       = entryCount > 0 ? backgroundEntries.AsSafePointer() : nint.Zero;
                count        = entryCount;
            }
        }
    }

    public override void GetLogoOverlayEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        isDisposable = false;
        handle = nint.Zero;
        count = 0;
        isAllocated = false;
    }

    public override void GetBackgroundFlag(out LauncherBackgroundFlag result)
        => result = LauncherBackgroundFlag.IsSourceFile | LauncherBackgroundFlag.TypeIsImage;

    public override void GetLogoFlag(out LauncherBackgroundFlag result)
        => result = LauncherBackgroundFlag.None;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        // If the next token is still in cache period, ignore.
        if (_nextRefreshToken > DateTimeOffset.Now)
            return 0;

        // Advance the next refresh token.
        _nextRefreshToken = DateTimeOffset.Now.Add(_refreshTokenTime);

        using HttpResponseMessage message = await ApiResponseHttpClient.GetAsync(ApiResponseBaseUrl + "api/launcher/base/config", HttpCompletionOption.ResponseHeadersRead, token);
        message.EnsureSuccessStatusCode();

#if USELIGHTWEIGHTJSONPARSER
        await using Stream networkStream = await message.Content.ReadAsStreamAsync(token);
        ApiResponse = await HBRApiResponse<HBRApiResponseMedia>.ParseFromAsync(networkStream, token: token);
#else
        string jsonResponse = await message.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger.LogTrace("API Media response: {JsonResponse}", jsonResponse);
        ApiResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseMedia>>(jsonResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseMedia);
#endif
        ApiResponse!.EnsureSuccessCode();
        await TryGetOverrideMediaAssets(token);

        return 0;
    }

    private async Task TryGetOverrideMediaAssets(CancellationToken token)
    {
        const string ListUrl = "remote-assets/background/override-background.json";

        try
        {
            using HttpResponseMessage? message = await ApiResponseHttpClient.GetResponseFromUrlOrCdnAsync(ListUrl, token: token);
            if (message == null)
            {
                SharedStatic.InstanceLogger.LogTrace("No CDNs are reachable :(");
                return;
            }

            if (!message.IsSuccessStatusCode)
            {
                SharedStatic.InstanceLogger.LogTrace("Override background is not available on: {url} (HTTP Status: {statusInt}/{status})",
                                                     ListUrl,
                                                     (int)message.StatusCode,
                                                     message.StatusCode);
                return;
            }

            await using Stream networkStream = await message.Content.ReadAsStreamAsync(token);
#if USELIGHTWEIGHTJSONPARSER
            using JsonDocument document = await JsonDocument.ParseAsync(networkStream, default, token).ConfigureAwait(false);
            ApiOverrideEntriesResponse = HBRApiResponseMediaOverrideEntry.ParseListFrom(document.RootElement);
#else
            ApiOverrideEntriesResponse = await JsonSerializer.DeserializeAsync<List<HBRApiResponseMediaOverrideEntry>>(
             networkStream, HBRApiResponseContext.Default.ListHBRApiResponseMediaOverrideEntry, token);
#endif

            // Check for reachable CDNs for the background URL.
            List<HBRApiResponseMediaOverrideEntry> availableEntries = [];
            foreach (HBRApiResponseMediaOverrideEntry entry in ApiOverrideEntriesResponse ?? [])
            {
                string? entryUri = entry.BackgroundUrl;
                if (string.IsNullOrEmpty(entryUri))
                    continue;

                using HttpResponseMessage? availableResponse =
                    await ApiResponseHttpClient.GetResponseFromUrlOrCdnAsync(entryUri, token: token);
                if (availableResponse == null)
                    continue;

                entry.BackgroundUrl = availableResponse.RequestMessage?.RequestUri?.ToString();
                SharedStatic.InstanceLogger.LogTrace("Override Background is found at: {url}", entry.BackgroundUrl);
                availableEntries.Add(entry);
            }

            ApiOverrideEntriesResponse = availableEntries;
            if (ApiOverrideEntriesResponse.Count == 0)
            {
                SharedStatic.InstanceLogger.LogTrace("Override Background List is currently empty or has no reachable entries");
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError(ex, "An error occurred while trying to gather override background list.");
        }
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
    }

    public override void Dispose()
    {
        using (ThisInstanceLock.EnterScope())
        {
            if (IsDisposed)
                return;

            ApiDownloadHttpClient.Dispose();
            ApiDownloadHttpClient = null!;

            ApiResponse = null;
            base.Dispose();
        }
    }
}
