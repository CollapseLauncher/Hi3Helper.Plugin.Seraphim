using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

[GeneratedComClass]
internal partial class HBRGameManager : GameManagerBase
{
    protected override HttpClient ApiResponseHttpClient { get; }
    protected          HttpClient ApiDownloadHttpClient { get; }
    protected override string     ApiResponseBaseUrl    { get; }

    private HBRApiResponse<HBRApiResponseGameConfig>?    ApiGameConfigResponse         { get; set; }
    private HBRApiResponse<HBRApiResponseGameConfigRef>? ApiGameDownloadRefResponse    { get; set; }
    private HBRGameLauncherConfig?                       CurrentGameConfig             { get; set; } = new();
    private string                                       CurrentGameTag                { get; }
    private string                                       CurrentGameExecutableByPreset { get; }

    internal string? GameResourceJsonUrl { get; set; }
    internal string? GameResourceBaseUrl { get; set; }

    internal HBRGameManager(string gameExecutableNameByPreset,
                            string apiResponseBaseUrl,
                            string gameTag,
                            string authSalt1,
                            string authSalt2)
    {
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
        ApiResponseBaseUrl            = apiResponseBaseUrl;
        ApiResponseHttpClient         = HBRUtility.CreateApiHttpClient(gameTag, true, true,  authSalt1, authSalt2);
        ApiDownloadHttpClient         = HBRUtility.CreateApiHttpClient(gameTag, true, false, authSalt1, authSalt2);
        CurrentGameTag                = gameTag;
    }

    public override void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient.Dispose();
            base.Dispose();
        }
    }

    private GameVersion? _currentGameVersion;
    protected override GameVersion CurrentGameVersion
    {
        get
        {
            if (_currentGameVersion.HasValue)
            {
                return _currentGameVersion.Value;
            }

            if (!GameVersion.TryParse(CurrentGameConfig?.Version, out _currentGameVersion))
            {
                _currentGameVersion = GameVersion.Empty;
            }

            return _currentGameVersion.Value;
        }
        set => _currentGameVersion = value;
    }

    private GameVersion? _apiGameVersion;
    protected override GameVersion ApiGameVersion
    {
        get
        {
            if (_apiGameVersion.HasValue)
            {
                return _apiGameVersion.Value;
            }

            if (!GameVersion.TryParse(ApiGameConfigResponse?.ResponseData?.CurrentVersion, out _apiGameVersion))
            {
                _apiGameVersion = GameVersion.Empty;
            }

            return _apiGameVersion.Value;
        }
        set => _apiGameVersion = value;
    }

    protected override bool HasPreload  => ApiPreloadGameVersion != GameVersion.Empty && !HasUpdate;
    protected override bool HasUpdate   => ApiGameVersion != CurrentGameVersion;
    protected override bool IsInstalled
    {
        get
        {
            string executablePath = Path.Combine(CurrentGameInstallPath ?? string.Empty, CurrentGameExecutableByPreset);
            return File.Exists(executablePath);
        }
    }

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion, bool isSave)
    {
        CurrentGameVersion = gameVersion;
        if (isSave)
        {
            SaveConfig();
        }
    }

    protected override void SetGamePathInner(string gamePath, bool isSave)
    {
        CurrentGameInstallPath = gamePath;
        if (isSave)
        {
            SaveConfig();
        }
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        // Retrieve Game Config API
        string gameConfigUrl = ApiResponseBaseUrl + "api/launcher/game/config";

        using HttpResponseMessage configMessage = await ApiResponseHttpClient.GetAsync(gameConfigUrl, HttpCompletionOption.ResponseHeadersRead, token);
        configMessage.EnsureSuccessStatusCode();

        string jsonConfigResponse = await configMessage.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger?.LogTrace("API GameConfig response: {JsonResponse}", jsonConfigResponse);

        ApiGameConfigResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseGameConfig>>(jsonConfigResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseGameConfig);
        ApiGameConfigResponse!.EnsureSuccessCode();

        if (ApiGameConfigResponse.ResponseData?.CurrentVersion == null)
        {
            throw new NullReferenceException("Game API Launcher cannot retrieve CurrentVersion value!");
        }

        if (ApiGameConfigResponse.ResponseData?.GameZipLocalPath == null)
        {
            throw new NullReferenceException("Game API Launcher cannot retrieve GameZipLocalPath reference value!");
        }

        // Retrieve Game Config Reference API
        string gameConfigRefUrl = gameConfigUrl + $"/json?version={ApiGameConfigResponse.ResponseData.CurrentVersion}&file_path={ApiGameConfigResponse.ResponseData.GameZipLocalPath}";

        using HttpResponseMessage configRefMessage = await ApiResponseHttpClient.GetAsync(gameConfigRefUrl, HttpCompletionOption.ResponseHeadersRead, token);
        configRefMessage.EnsureSuccessStatusCode();

        string jsonConfigRefResponse = await configRefMessage.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger?.LogTrace("API GameConfigRef response: {JsonResponse}", jsonConfigRefResponse);

        ApiGameDownloadRefResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseGameConfigRef>>(jsonConfigRefResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseGameConfigRef);
        ApiGameDownloadRefResponse!.EnsureSuccessCode();

        GameResourceJsonUrl = ApiGameDownloadRefResponse.ResponseData?.DownloadAssetsReferenceUrl ?? throw new NullReferenceException("Game API Launcher cannot retrieve DownloadAssetsReferenceUrl value!");

        Uri gameResourceBase = new Uri(GameResourceJsonUrl);
        GameResourceBaseUrl = $"{gameResourceBase.Scheme}://{gameResourceBase.Host}";

        // Set API current game version
        if (!GameVersion.TryParse(ApiGameConfigResponse.ResponseData.CurrentVersion, out GameVersion? currentVersion))
        {
            throw new InvalidOperationException($"API GameConfig returns an invalid CurrentVersion data! Data: {ApiGameConfigResponse.ResponseData.CurrentVersion}");
        }
        ApiGameVersion = currentVersion.Value;

        // Load the config
        LoadConfig();

        return 0;
    }

    protected override Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, byte[]? fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        return base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
    }

    internal HttpClient GetDownloadClient() => ApiDownloadHttpClient; // TODO: Use this to pass the HttpClient to the IGameInstaller instance.

    private void LoadConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            return;
        }

        string   filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            using FileStream fileStream = fileInfo.OpenRead();
            CurrentGameConfig           = JsonSerializer.Deserialize(fileStream, HBRGameLauncherConfigContext.Default.HBRGameLauncherConfig);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger?.LogError("Cannot load game-launcher-config.json! Reason: {Exception}", ex);
        }
    }

    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            return;
        }

        string   filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        fileInfo.Directory?.Create();
        if (fileInfo.Exists)
        {
            fileInfo.IsReadOnly = false;
        }

        using FileStream      fileStream   = fileInfo.Create();
        HBRGameLauncherConfig configToSave = new HBRGameLauncherConfig
        {
            ExecutableName = ApiGameConfigResponse?.ResponseData?.GameExecutableFileName ?? Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset),
            GameTag        = CurrentGameTag,
            Version        = CurrentGameVersion.ToString()
        };

        CurrentGameConfig = configToSave;
        JsonSerializer.Serialize(fileStream, configToSave, HBRGameLauncherConfigContext.Default.HBRGameLauncherConfig);
    }
}
