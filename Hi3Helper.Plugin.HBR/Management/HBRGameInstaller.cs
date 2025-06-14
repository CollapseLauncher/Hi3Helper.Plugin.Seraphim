/* =======================================================================================================================
 * NOTE FOR PLUGIN DEVELOPERS:
 * =======================================================================================================================
 * 
 * Due to unique implementation of the game installation mechanism for each game, the core library only provides a base
 * class in which used as a helper to invoke basic Async Calls to methods provided by IGameInstaller and
 * IGameUninstaller COM Interfaces. The developer "MUST" implement their game installation mechanism "FROM SCRATCH".
 * 
 * The developer is also required to create HttpClient instance by using Core Library's PluginHttpClientBuilder and
 * use SocketsHttpHandler in-order to ensure connection reusability and support for Proxy and External DNS resolving
 * callback provided by the main Collapse Launcher app.
 * 
 * See: https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/9.0/default-handler#reason-for-change
 * 
 * Also, it's important to "DISPOSE" HttpClient instance once the plugin is being freed by the main Collapse
 * Launcher app.
 * 
 */

using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.HBR.Management;

[GeneratedComClass]
// ReSharper disable once InconsistentNaming
public partial class HBRGameInstaller : GameInstallerBase
{
    private const double ExCacheDurationInMinute = 10d;

    private DateTimeOffset _cacheExpiredUntil = DateTimeOffset.MinValue;

    private string? GameManifestUrl    => (GameManager as HBRGameManager)?.GameResourceJsonUrl;
    private string? GameAssetBaseUrl   => (GameManager as HBRGameManager)?.GameResourceBaseUrl;
    private string? GameAssetBasisPath => (GameManager as HBRGameManager)?.GameResourceBasisPath;

    private HBRApiGameInstallManifest? _currentGameAssetManifest;
    private HBRApiGameInstallManifest? _preloadGameAssetManifest;

    private readonly HttpClient _downloadHttpClient;

    internal HBRGameInstaller(IGameManager? gameManager) : base(gameManager)
    {
        _downloadHttpClient = new PluginHttpClientBuilder<SocketsHttpHandler>()
            .AllowUntrustedCert()
            .AllowCookies()
            .SetAllowedDecompression()
            .SetMaxConnection(512)
            .Create();
    }

    protected override async Task<long> GetGameSizeAsyncInner(
        GameInstallerKind gameInstallerKind,
        CancellationToken token)
    {
        if (_currentGameAssetManifest == null)
        {
            return 0L;
        }

        // Ensure the data is always been initialized
        await InitAsync(token);

        return gameInstallerKind switch
        {
            GameInstallerKind.None => 0,
            GameInstallerKind.Install or GameInstallerKind.Update => _currentGameAssetManifest.GameAssets?.Sum(x => x.AssetSize) ?? 0L,
            GameInstallerKind.Preload => _preloadGameAssetManifest?.GameAssets?.Sum(x => x.AssetSize) ?? 0L,
            _ => throw new InvalidOperationException()
        };
    }

    protected override async Task<long> GetGameDownloadedSizeAsyncInner(
        GameInstallerKind gameInstallerKind,
        CancellationToken token)
    {
        if (_currentGameAssetManifest == null)
        {
            return 0L;
        }

        // Ensure the data is always been initialized
        await InitAsync(token);

        return await Task.Factory.StartNew(() =>
        {
            string gamePath = EnsureAndGetGamePath();

            return gameInstallerKind switch
            {
                GameInstallerKind.None => 0,
                GameInstallerKind.Install or GameInstallerKind.Update => _currentGameAssetManifest.GameAssets?.Sum(x =>
                {
                    if (string.IsNullOrEmpty(x.AssetPath))
                    {
                        return 0;
                    }

                    string filePath = Path.Combine(gamePath, x.AssetPath.TrimStart("/\\").ToString());
                    FileInfo fileInfo = new FileInfo(filePath);
                    return !fileInfo.Exists || fileInfo.Length != x.AssetSize ? 0 : fileInfo.Length;
                }) ?? 0L,
                GameInstallerKind.Preload => _preloadGameAssetManifest?.GameAssets?.Sum(x => x.AssetSize) ?? 0L,
                _ => throw new InvalidOperationException()
            };
        }, token);
    }

    protected override async Task StartInstallAsyncInner(
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        CancellationToken token)
    {
        // Show preparing state and perform update
        progressStateDelegate?.Invoke(InstallProgressState.Preparing);

        // Ensure the data is always been initialized
        await InitAsync(token);

        // Create progress struct
        InstallProgress progressStruct = new InstallProgress
        {
            StateCount = 1,
            TotalStateToComplete = 1
        };

        // Get the path
        string gamePath = EnsureAndGetGamePath();
        await StartInstallAsyncInner(
            gamePath,
            progressStruct,
            _currentGameAssetManifest?.GameAssets ?? throw new NullReferenceException("_currentGameAssetManifest?.GameAssets is null!"),
            _currentGameAssetManifest?.RootSuffixPath ?? "",
            progressDelegate,
            progressStateDelegate,
            false,
            token);

        // We need to set the game path and set the version of the current game. So, save the configuration.
        GameManager.GetApiGameVersion(out GameVersion latestVersion);
        GameManager.SetCurrentGameVersion(latestVersion);
        GameManager.SaveConfig();

        // Write manifest
        await WriteCachedManifestFile(token);
    }

    protected override async Task StartUpdateAsyncInner(
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        CancellationToken token)
    {
        // Show preparing state and perform update
        progressStateDelegate?.Invoke(InstallProgressState.Preparing);

        // Ensure the data is always been initialized
        await InitAsync(token);

        // Get the path and perform validation
        string gamePath = EnsureAndGetGamePath();
        List<GameInstallAsset> needUpdateAssets = await StartGetMismatchedAssets(
            gamePath,
            new InstallProgress
            {
                StateCount = 1,
                TotalStateToComplete = 2
            },
            _currentGameAssetManifest?.GameAssets ?? throw new NullReferenceException("_currentGameAssetManifest?.GameAssets is null!"),
            progressDelegate,
            progressStateDelegate,
            token);

        // Then start the update
        await StartInstallAsyncInner(
            gamePath,
            new InstallProgress
            {
                StateCount = 2,
                TotalStateToComplete = 2
            },
            needUpdateAssets,
            _currentGameAssetManifest?.RootSuffixPath ?? "",
            progressDelegate,
            progressStateDelegate,
            true,
            token);

        // We need to set the game path and set the version of the current game. So, save the configuration.
        GameManager.GetApiGameVersion(out GameVersion latestVersion);
        GameManager.SetCurrentGameVersion(latestVersion);
        GameManager.SaveConfig();

        // Write manifest
        await WriteCachedManifestFile(token);
    }

    protected override async Task StartPreloadAsyncInner(
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        CancellationToken token)
    {
        // Ensure the data is always been initialized
        await InitAsync(token);

        throw new NotImplementedException();
    }

    protected override Task UninstallAsyncInner(
        CancellationToken token)
    {
        return Task.CompletedTask;
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        bool isNeedSync = IsCacheExpired();

        HBRGameManager asHbrGameManager = GameManager as HBRGameManager ?? throw new InvalidOperationException("IGameManager is not HBRGameManager!");
        await asHbrGameManager.InitAsyncInner(isNeedSync, token);

        if (!isNeedSync && _currentGameAssetManifest != null)
        {
            return 0;
        }

        HttpClient client = asHbrGameManager.GetDownloadClient();
        if (client == null)
        {
            throw new NullReferenceException("Cannot use HttpClient from IGameManager.GetDownloadClient() because it's null!");
        }

        if (string.IsNullOrEmpty(GameManifestUrl))
        {
            throw new NullReferenceException("The game manifest URL is null or empty!");
        }

        using HttpResponseMessage message = await client.GetAsync(GameManifestUrl, HttpCompletionOption.ResponseHeadersRead, token);
        if (!message.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Cannot retrieve the manifest from URL: {GameManifestUrl} with status code: {message.StatusCode} ({(int)message.StatusCode})",
                null,
                message.StatusCode);
        }

        _currentGameAssetManifest = await message.Content.ReadFromJsonAsync(HBRApiGameInstallManifestContext.Default.HBRApiGameInstallManifest, token);

        // TODO: Implement preload manifest load mechanism.

        UpdateCacheExpiration();

        return 0;
    }

    private async Task WriteCachedManifestFile(CancellationToken token)
    {
        if (_currentGameAssetManifest?.GameAssets == null ||
            _currentGameAssetManifest.GameAssets.Count == 0 ||
            GameManager is not HBRGameManager manager)
        {
            return;
        }

        manager.GetApiGameVersion(out GameVersion latestVersion);
        if (latestVersion == GameVersion.Empty)
        {
            return;
        }

        string filePath = Path.Combine(EnsureAndGetGamePath(), "manifest.json");
        FileInfo fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists)
        {
            fileInfo.IsReadOnly = false;
        }
        fileInfo.Directory?.Create();

        HBRGameManifest manifest = await Task.Factory.StartNew(() =>
        {
            return new HBRGameManifest
            {
                GamePackageBasis = GameAssetBasisPath,
                GameTag = manager.CurrentGameTag,
                GameVersion = latestVersion.ToString(),
                ManifestEntries = _currentGameAssetManifest.GameAssets.Select(x => new HBRGameManifestEntry
                {
                    AssetCrc64Hash = x.AssetHash,
                    AssetPath = x.AssetPath,
                    AssetSize = x.AssetSize,
                }).ToList()
            };
        }, token);

        await using FileStream stream = fileInfo.Create();
        JavaScriptEncoder jsonEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        JsonWriterOptions jsonWriterOptionsIndented = new()
        {
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
            Encoder = jsonEncoder
        };

        await using Utf8JsonWriter jsonWriterIndented = new(stream, jsonWriterOptionsIndented);
        await Task.Factory.StartNew(() =>
            JsonSerializer.Serialize(jsonWriterIndented, manifest, HBRGameLauncherConfigContext.Default.HBRGameManifest),
            token);
    }

    private bool IsCacheExpired()        => DateTimeOffset.UtcNow > _cacheExpiredUntil;
    private void UpdateCacheExpiration() => _cacheExpiredUntil = DateTimeOffset.UtcNow.AddMinutes(ExCacheDurationInMinute);

    public override void Dispose()
    {
        _downloadHttpClient.Dispose();

        GC.SuppressFinalize(this);
    }
}
