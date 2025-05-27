using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Hi3Helper.Plugin.Core.Utility;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiNews : LauncherApiNewsBase, ILauncherApiNews
{
    protected override HttpClient ApiResponseHttpClient { get; }
    protected          HttpClient ApiDownloadHttpClient { get; }

    private HBRApiResponse<HBRApiResponseSocial>? SocialApiResponse { get; set; }
    private string ApiBaseUrl { get; }

    internal HBRGlobalLauncherApiNews(string apiBaseUrl, string gameTag, string authSalt1, string authSalt2)
    {
        ApiBaseUrl = apiBaseUrl;
        ApiResponseHttpClient = HBRUtility.CreateApiHttpClient(gameTag, true, true, authSalt1, authSalt2);
        ApiDownloadHttpClient = HBRUtility.CreateApiHttpClient(gameTag, false, false);
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        using HttpResponseMessage message = await ApiResponseHttpClient.GetAsync(ApiBaseUrl + "api/launcher/social/media/resource", HttpCompletionOption.ResponseHeadersRead, token);
        message.EnsureSuccessStatusCode();

        string jsonResponse = await message.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger?.LogTrace("API Social Media and News response: {JsonResponse}", jsonResponse);

        SocialApiResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseSocial>>(jsonResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseSocial);
        SocialApiResponse!.EnsureSuccessCode();

        // Initialize embedded Icon data
        await HBRIconData.Initialize(token);

        return !message.IsSuccessStatusCode ? (int)message.StatusCode : 0;
    }

    public override bool GetNewsEntries(out nint handle, out int count, out bool isDisposable)
        => InitializeEmpty(out handle, out count, out isDisposable);

    public override bool GetCarouselEntries(out nint handle, out int count, out bool isDisposable)
        => InitializeEmpty(out handle, out count, out isDisposable);

    public override unsafe bool GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable)
    {
        try
        {
            if (SocialApiResponse?.ResponseData?.SocialMediaEntries == null ||
                SocialApiResponse.ResponseData.SocialMediaEntries.Count == 0)
            {
                return InitializeEmpty(out handle, out count, out isDisposable);
            }

            List<HBRApiResponseSocialResponse> validEntries = [..SocialApiResponse.ResponseData.SocialMediaEntries
                .Where(x => !string.IsNullOrEmpty(x.SocialMediaName) &&
                            !string.IsNullOrEmpty(x.ClickUrl) &&
                            HBRIconData.EmbeddedDataDictionary.ContainsKey(x.SocialMediaName)
                )];

            int entryCount = validEntries.Count;
            PluginDisposableMemory<LauncherSocialMediaEntry> memory = PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(entryCount);

            handle = memory.AsSafePointer();
            count = entryCount;
            isDisposable = true;

            SharedStatic.InstanceLogger?.LogTrace("[HBRGlobalLauncherApiNews::GetSocialMediaEntries] {EntryCount} entries are allocated at: 0x{Address:x8}", entryCount, handle);

            for (int i = 0; i < entryCount; i++)
            {
                string socialMediaName = validEntries[i].SocialMediaName!;
                string clickUrl = validEntries[i].ClickUrl!;
                string? qrImageUrl = validEntries[i].QrImageUrl;

                byte[]? iconData = HBRIconData.GetEmbeddedData(socialMediaName);
                if (iconData == null)
                {
                    continue;
                }

                void* iconDataPtr = MemoryMarshal.GetArrayDataReference(iconData).AsPointer();
                int iconDataLength = iconData.Length;

                LauncherSocialMediaEntryFlag flags = LauncherSocialMediaEntryFlag.IconIsDataBuffer |
                                                     LauncherSocialMediaEntryFlag.HasClickUrl |
                                                     LauncherSocialMediaEntryFlag.HasDescription;

                ref LauncherSocialMediaEntry unmanagedEntry = ref memory[i];
                if (!string.IsNullOrEmpty(qrImageUrl))
                {
                    flags |= LauncherSocialMediaEntryFlag.QrImageIsPath | LauncherSocialMediaEntryFlag.HasQrImage;
                }

                unmanagedEntry.InitInner(iconDataPtr,
                                         iconDataLength,
                                         false,
                                         null,
                                         0,
                                         false,
                                         null,
                                         0,
                                         false,
                                         null,
                                         flags);

                socialMediaName.CopyToUtf8(unmanagedEntry.SocialMediaDescription);
                clickUrl.CopyToUtf8(unmanagedEntry.SocialMediaClickUrl);
            }

            return true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger?.LogError(ex, "Failed to get social media entries");
            return InitializeEmpty(out handle, out count, out isDisposable);
        }
    }

    private static bool InitializeEmpty(out nint handle, out int count, out bool isDisposable)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;

        return false;
    }

    public override void Dispose()
    {
        base.Dispose();
        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient.Dispose();
            SocialApiResponse = null;
        }
    }
}
