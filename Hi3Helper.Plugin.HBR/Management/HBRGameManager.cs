using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.HBR.Management;

[GeneratedComClass]
internal partial class HBRGameManager : GameManagerBase
{
    [field: AllowNull, MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(CurrentGameTag, true, true, CurrentAuthSalt1, CurrentAuthSalt2);
        set;
    }
        
    [field: AllowNull, MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(CurrentGameTag, true, false, CurrentAuthSalt1, CurrentAuthSalt2);
        set;
    }

    protected override string ApiResponseBaseUrl { get; }

    private HBRApiResponse<HBRApiResponseGameConfig>?    ApiGameConfigResponse         { get; set; }
    private HBRApiResponse<HBRApiResponseGameConfigRef>? ApiGameDownloadRefResponse    { get; set; }
    private JsonObject                                   CurrentGameConfigNode         { get; set; } = new();
    private string                                       CurrentGameTag                { get; }
    private string                                       CurrentGameExecutableByPreset { get; }
    private string                                       CurrentAuthSalt1              { get; }
    private string                                       CurrentAuthSalt2              { get; }
    private string?                                      CurrentGameLauncherUninstKey  { get; }

    internal string? GameResourceJsonUrl { get; set; }
    internal string? GameResourceBaseUrl { get; set; }
    internal bool    IsInitialized       { get; set; }

    internal HBRGameManager(string gameExecutableNameByPreset,
                            string apiResponseBaseUrl,
                            string gameTag,
                            string authSalt1,
                            string authSalt2,
                            string? launcherUninstallKey)
    {
        CurrentGameLauncherUninstKey  = launcherUninstallKey;
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
        ApiResponseBaseUrl            = apiResponseBaseUrl;
        CurrentAuthSalt1              = authSalt1;
        CurrentAuthSalt2              = authSalt2;
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
            ApiDownloadHttpClient = null!;

            ApiGameConfigResponse = null;
            ApiGameDownloadRefResponse = null;
            base.Dispose();
        }
    }

    protected override GameVersion CurrentGameVersion
    {
        get
        {
            string? version = CurrentGameConfigNode.GetConfigValue<string?>("version");
            if (version == null)
            {
                return GameVersion.Empty;
            }

            if (!GameVersion.TryParse(version, out GameVersion? currentGameVersion))
            {
                currentGameVersion = GameVersion.Empty;
            }

            return currentGameVersion.Value;
        }
        set => CurrentGameConfigNode.SetConfigValue("version", value.ToString());
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
    protected override bool HasUpdate   => IsInstalled && ApiGameVersion != CurrentGameVersion;
    protected override bool IsInstalled
    {
        get
        {
            string executablePath = Path.Combine(CurrentGameInstallPath ?? string.Empty, CurrentGameExecutableByPreset);
            return File.Exists(executablePath);
        }
    }

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion) => CurrentGameVersion = gameVersion;

    protected override void SetGamePathInner(string gamePath) => CurrentGameInstallPath = gamePath;

    protected override Task<int> InitAsync(CancellationToken token) => InitAsyncInner(true, token);

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized)
        {
            return 0;
        }

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
        IsInitialized  = true;

        return 0;
    }

    protected override Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        return base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
    }

    [GeneratedRegex("^\"?(?<path>[^\"]+?\\.exe)\"?")]
    public static partial Regex FilterUninstallKeyRegexMatch();

    // TODO: Implement the existing game install path search logic.
#pragma warning disable CA1416
    protected override Task<string?> FindExistingInstallPathAsyncInner(CancellationToken token)
        => Task.Factory.StartNew<string?>(() =>
        {
            if (FindRegistryPath() is not { } rootKey)
                return null;

            if (rootKey.GetValue("UninstallString") is not string pathString)
            {
#if DEBUG
                SharedStatic.InstanceLogger?.LogTrace("Type of the value from Registry Key is not a string!");
#endif
                return null;
            }

            Match regexMatch = FilterUninstallKeyRegexMatch().Match(pathString);
            if (regexMatch.Success)
            {
                pathString = regexMatch.Groups["path"].Value;
            }

            if (!Path.Exists(pathString))
            {
#if DEBUG
                SharedStatic.InstanceLogger?.LogTrace("Cannot find uninstall path at: {Path}", pathString);
#endif
            }

            string? rootSearchPath = Path.GetDirectoryName(Path.GetDirectoryName(pathString));
            if (string.IsNullOrEmpty(rootSearchPath))
                return null;

            // ReSharper disable once LoopCanBeConvertedToQuery
            string gameName = Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset);
#if DEBUG
            SharedStatic.InstanceLogger?.LogTrace("Start finding game existing installation using prefix: {PrefixName} from root path: {RootPath}", gameName, rootSearchPath);
#endif
            foreach (string dirPath in Directory.EnumerateDirectories(rootSearchPath, $"{gameName}", SearchOption.AllDirectories))
            {
#if DEBUG
                SharedStatic.InstanceLogger?.LogTrace("Checking for game presence in directory: {DirPath}", dirPath);
#endif
                foreach (string path in Directory.EnumerateFiles(dirPath, $"*{gameName}*", SearchOption.TopDirectoryOnly))
                {
#if DEBUG
                    SharedStatic.InstanceLogger?.LogTrace("Got executable file at: {ExecPath}", path);
#endif
                    string? parentPath = Path.GetDirectoryName(path);
                    if (parentPath == null)
                        continue;

                    string jsonPath = Path.Combine(parentPath, "game-launcher-config.json");
                    if (File.Exists(jsonPath))
                    {
                        return parentPath;
                    }
                }
            }

            return null;
        }, token);

    private RegistryKey? FindRegistryPath()
    {
        const string Wow6432Node = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\";
        if (string.IsNullOrEmpty(CurrentGameLauncherUninstKey))
        {
            return null;
        }

        using RegistryKey? wow6432RootKey = Registry.LocalMachine.OpenSubKey(Wow6432Node);
        RegistryKey?       launcherKey = wow6432RootKey?.OpenSubKey(CurrentGameLauncherUninstKey);

#if DEBUG
        if (launcherKey == null)
        {
            SharedStatic.InstanceLogger?.LogTrace("Cannot find registry key: {Key} from parent path: {Parent}", CurrentGameLauncherUninstKey, Wow6432Node);
        }
        else
        {
            SharedStatic.InstanceLogger?.LogTrace("Found registry key: {Key} from parent path: {Parent}", CurrentGameLauncherUninstKey, Wow6432Node);
        }
#endif

        return launcherKey;
    }
#pragma warning restore CA1416

    internal HttpClient GetDownloadClient() => ApiDownloadHttpClient; // TODO: Use this to pass the HttpClient to the IGameInstaller instance.

    public override void LoadConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            SharedStatic.InstanceLogger?.LogWarning("[HBRGameManager::LoadConfig] Game directory isn't set! Game config won't be loaded.");
            return;
        }

        string   filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SharedStatic.InstanceLogger?.LogWarning("[HBRGameManager::LoadConfig] File game-launcher-config.json doesn't exist on dir: {Dir}", CurrentGameInstallPath);
            return;
        }

        try
        {
            using FileStream fileStream = fileInfo.OpenRead();
            CurrentGameConfigNode = JsonNode.Parse(fileStream) as JsonObject ?? new JsonObject();
            SharedStatic.InstanceLogger?.LogTrace("[HBRGameManager::LoadConfig] Loaded game-launcher-config.json from directory: {Dir}", CurrentGameInstallPath);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger?.LogError("[HBRGameManager::LoadConfig] Cannot load game-launcher-config.json! Reason: {Exception}", ex);
        }
    }

    public override void SaveConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            SharedStatic.InstanceLogger?.LogWarning("[HBRGameManager::LoadConfig] Game directory isn't set! Game config won't be saved.");
            return;
        }

        CurrentGameConfigNode.SetConfigValueIfEmpty("tag", CurrentGameTag);
        CurrentGameConfigNode.SetConfigValueIfEmpty("name", ApiGameConfigResponse?.ResponseData?.GameExecutableFileName ?? Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset));
        if (CurrentGameVersion == GameVersion.Empty)
        {
            SharedStatic.InstanceLogger?.LogWarning("[HBRGameManager::SaveConfig] Current version returns 0.0.0! Overwrite the version to current provided version by API, {VersionApi}", ApiGameVersion);
            CurrentGameVersion = ApiGameVersion;
        }

        string vcSalt = HBRGameLauncherConfig
            .GetConfigSalt(CurrentGameConfigNode.GetConfigValue<string?>("name"),
                           CurrentGameConfigNode.GetConfigValue<string?>("tag"),
                           CurrentGameConfigNode.GetConfigValue<string?>("version"));
        CurrentGameConfigNode.SetConfigValue("vc", vcSalt);

        string filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        fileInfo.Directory?.Create();
        if (fileInfo.Exists)
        {
            fileInfo.IsReadOnly = false;
        }

        using FileStream fileStream = fileInfo.Open(FileMode.Create);
        using Utf8JsonWriter writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            IndentCharacter = ' ',
            NewLine = "\n"
        });

        CurrentGameConfigNode.WriteTo(writer);
        SharedStatic.InstanceLogger?.LogTrace("[HBRGameManager::SaveConfig] Saved game-launcher-config.json to directory: {Dir}", CurrentGameInstallPath);
    }
}
