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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Nodes;
using Hi3Helper.Plugin.Core.Utility.Json;
#endif

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

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
    private string                                       CurrentGameExecutableByPreset { get; }
    private string                                       CurrentAuthSalt1              { get; }
    private string                                       CurrentAuthSalt2              { get; }
    private string?                                      CurrentGameLauncherUninstKey  { get; }

#if USELIGHTWEIGHTJSONPARSER
    private HBRGameLauncherConfig CurrentGameConfig { get; set; } = HBRGameLauncherConfig.CreateEmpty();
#else
    private JsonObject CurrentGameConfigNode { get; set; } = new();
#endif

    internal string CurrentGameTag { get; }

    internal string? GameResourceJsonUrl   { get; set; }
    internal string? GameResourceBaseUrl   { get; set; }
    internal string? GameResourceBasisPath { get; set; }
    internal bool    IsInitialized         { get; set; }

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
#if !USELIGHTWEIGHTJSONPARSER
        get
        {
            string? version = CurrentGameConfigNode.GetConfigValue<string?>("version");
            if (version == null)
            {
                return GameVersion.Empty;
            }

            if (!GameVersion.TryParse(version, null, out GameVersion currentGameVersion))
            {
                currentGameVersion = GameVersion.Empty;
            }

            return currentGameVersion;
        }
        set => CurrentGameConfigNode.SetConfigValue("version", value.ToString());
#else
        get => CurrentGameConfig.Version;
        set => CurrentGameConfig.Version = value;
#endif
    }

    protected override GameVersion ApiGameVersion
    {
        get
        {
            if (ApiGameConfigResponse?.ResponseData == null)
            {
                return GameVersion.Empty;
            }

            field = ApiGameConfigResponse.ResponseData.CurrentVersion;
            return field;
        }
        set;
    }

    protected override bool HasPreload  => ApiPreloadGameVersion != GameVersion.Empty && !HasUpdate;
    protected override bool HasUpdate   => IsInstalled && ApiGameVersion != CurrentGameVersion;
    protected override bool IsInstalled
    {
        get
        {
            string executablePath1 = Path.Combine(CurrentGameInstallPath ?? string.Empty, CurrentGameExecutableByPreset);
            string executablePath2 = Path.Combine(CurrentGameInstallPath ?? string.Empty, Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset) + "_Data", "globalgamemanagers");
            string executablePath3 = Path.Combine(CurrentGameInstallPath ?? string.Empty, Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset) + "_Data", "app.info");
            string executablePath4 = Path.Combine(CurrentGameInstallPath ?? string.Empty, "UnityPlayer.dll");
            string executablePath5 = Path.Combine(CurrentGameInstallPath ?? string.Empty, "GameAssembly.dll");
            string executablePath6 = Path.Combine(CurrentGameInstallPath ?? string.Empty, "game-launcher-config.json");
            string executablePath7 = Path.Combine(CurrentGameInstallPath ?? string.Empty, "manifest.json");
            return File.Exists(executablePath1) &&
                   File.Exists(executablePath2) &&
                   File.Exists(executablePath3) &&
                   File.Exists(executablePath4) &&
                   File.Exists(executablePath5) &&
                   File.Exists(executablePath6) &&
                   File.Exists(executablePath7);
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

#if USELIGHTWEIGHTJSONPARSER
        await using Stream networkStreamConfig = await configMessage.Content.ReadAsStreamAsync(token);
        ApiGameConfigResponse = await HBRApiResponse<HBRApiResponseGameConfig>.ParseFromAsync(networkStreamConfig, token: token);
#else
        string jsonConfigResponse = await configMessage.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger.LogTrace("API GameConfig response: {JsonResponse}", jsonConfigResponse);
        ApiGameConfigResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseGameConfig>>(jsonConfigResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseGameConfig);
#endif

        ApiGameConfigResponse!.EnsureSuccessCode();

        if (ApiGameConfigResponse.ResponseData == null)
        {
            throw new NullReferenceException("ApiGameConfigResponse.ResponseData returns null!");
        }

        if (ApiGameConfigResponse.ResponseData.CurrentVersion == GameVersion.Empty)
        {
            throw new NullReferenceException("Game API Launcher cannot retrieve CurrentVersion value!");
        }

        GameResourceBasisPath = ApiGameConfigResponse.ResponseData.GameZipLocalPath;
        if (GameResourceBasisPath == null)
        {
            throw new NullReferenceException("Game API Launcher cannot retrieve GameZipLocalPath reference value!");
        }

        // Retrieve Game Config Reference API
        string gameConfigRefUrl = gameConfigUrl + $"/json?version={ApiGameConfigResponse.ResponseData.CurrentVersion.ToString("N")}&file_path={GameResourceBasisPath}";

        using HttpResponseMessage configRefMessage = await ApiResponseHttpClient.GetAsync(gameConfigRefUrl, HttpCompletionOption.ResponseHeadersRead, token);
        configRefMessage.EnsureSuccessStatusCode();

#if USELIGHTWEIGHTJSONPARSER
        await using Stream networkStreamConfigRef = await configRefMessage.Content.ReadAsStreamAsync(token);
        ApiGameDownloadRefResponse = await HBRApiResponse<HBRApiResponseGameConfigRef>.ParseFromAsync(networkStreamConfigRef, token: token);
#else
        string jsonConfigRefResponse = await configRefMessage.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger.LogTrace("API GameConfigRef response: {JsonResponse}", jsonConfigRefResponse);
        ApiGameDownloadRefResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseGameConfigRef>>(jsonConfigRefResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseGameConfigRef);
#endif

        ApiGameDownloadRefResponse?.EnsureSuccessCode();

        if (ApiGameDownloadRefResponse?.ResponseData == null)
        {
            throw new NullReferenceException("ApiGameDownloadRefResponse.ResponseData returns null!");
        }

        GameResourceJsonUrl = ApiGameDownloadRefResponse.ResponseData.DownloadAssetsReferenceUrl ?? throw new NullReferenceException("Game API Launcher cannot retrieve DownloadAssetsReferenceUrl value!");

        Uri gameResourceBase = new Uri(GameResourceJsonUrl);
        GameResourceBaseUrl = $"{gameResourceBase.Scheme}://{gameResourceBase.Host}";

        // Set API current game version
        if (ApiGameConfigResponse.ResponseData.CurrentVersion == GameVersion.Empty)
        {
            throw new InvalidOperationException($"API GameConfig returns an invalid CurrentVersion data! Data: {ApiGameConfigResponse.ResponseData.CurrentVersion}");
        }

        ApiGameVersion = ApiGameConfigResponse.ResponseData.CurrentVersion;
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
                SharedStatic.InstanceLogger.LogTrace("Type of the value from Registry Key is not a string!");
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
                SharedStatic.InstanceLogger.LogTrace("Cannot find uninstall path at: {Path}", pathString);
#endif
            }

            string? rootSearchPath = Path.GetDirectoryName(Path.GetDirectoryName(pathString));
            if (string.IsNullOrEmpty(rootSearchPath))
                return null;

            // ReSharper disable once LoopCanBeConvertedToQuery
            string gameName = Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset);
#if DEBUG
            SharedStatic.InstanceLogger.LogTrace("Start finding game existing installation using prefix: {PrefixName} from root path: {RootPath}", gameName, rootSearchPath);
#endif
            foreach (string dirPath in Directory.EnumerateDirectories(rootSearchPath, $"{gameName}", SearchOption.AllDirectories))
            {
#if DEBUG
                SharedStatic.InstanceLogger.LogTrace("Checking for game presence in directory: {DirPath}", dirPath);
#endif
                foreach (string path in Directory.EnumerateFiles(dirPath, $"*{gameName}*", SearchOption.TopDirectoryOnly))
                {
#if DEBUG
                    SharedStatic.InstanceLogger.LogTrace("Got executable file at: {ExecPath}", path);
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
        SharedStatic.InstanceLogger.LogTrace(
            launcherKey == null
                ? "Cannot find registry key: {Key} from parent path: {Parent}"
                : "Found registry key: {Key} from parent path: {Parent}", CurrentGameLauncherUninstKey, Wow6432Node);
#endif

        return launcherKey;
    }
#pragma warning restore CA1416

    internal HttpClient GetDownloadClient() => ApiDownloadHttpClient; // TODO: Use this to pass the HttpClient to the IGameInstaller instance.

    public override void LoadConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            SharedStatic.InstanceLogger.LogWarning("[HBRGameManager::LoadConfig] Game directory isn't set! Game config won't be loaded.");
            return;
        }

        string   filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SharedStatic.InstanceLogger.LogWarning("[HBRGameManager::LoadConfig] File game-launcher-config.json doesn't exist on dir: {Dir}", CurrentGameInstallPath);
            return;
        }

        try
        {
            using FileStream fileStream = fileInfo.OpenRead();
#if USELIGHTWEIGHTJSONPARSER
            CurrentGameConfig = HBRGameLauncherConfig.ParseFrom(fileStream);
#else
            CurrentGameConfigNode = JsonNode.Parse(fileStream) as JsonObject ?? new JsonObject();
#endif
            SharedStatic.InstanceLogger.LogTrace("[HBRGameManager::LoadConfig] Loaded game-launcher-config.json from directory: {Dir}", CurrentGameInstallPath);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError("[HBRGameManager::LoadConfig] Cannot load game-launcher-config.json! Reason: {Exception}", ex);
        }
    }

    public override void SaveConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            SharedStatic.InstanceLogger.LogWarning("[HBRGameManager::LoadConfig] Game directory isn't set! Game config won't be saved.");
            return;
        }

#if !USELIGHTWEIGHTJSONPARSER
        CurrentGameConfigNode.SetConfigValueIfEmpty("tag", CurrentGameTag);
        CurrentGameConfigNode.SetConfigValueIfEmpty("name", ApiGameConfigResponse?.ResponseData?.GameExecutableFileName ?? Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset));
#endif
        if (CurrentGameVersion == GameVersion.Empty)
        {
            SharedStatic.InstanceLogger.LogWarning("[HBRGameManager::SaveConfig] Current version returns 0.0.0! Overwrite the version to current provided version by API, {VersionApi}", ApiGameVersion);
            CurrentGameVersion = ApiGameVersion;
        }

#if !USELIGHTWEIGHTJSONPARSER
        string vcSalt = HBRGameLauncherConfig
            .GetConfigSalt(CurrentGameConfigNode.GetConfigValue<string?>("tag"),
                           CurrentGameConfigNode.GetConfigValue<string?>("name"),
                           CurrentGameConfigNode.GetConfigValue<string?>("version"));
        CurrentGameConfigNode.SetConfigValue("vc", vcSalt);
#else
        CurrentGameConfig.Tag  = CurrentGameTag;
        CurrentGameConfig.Name = ApiGameConfigResponse?.ResponseData?.GameExecutableFileName ?? CurrentGameExecutableByPreset;
#endif

        string filePath = Path.Combine(CurrentGameInstallPath, "game-launcher-config.json");
        FileInfo fileInfo = new FileInfo(filePath);

        fileInfo.Directory?.Create();
        if (fileInfo.Exists)
        {
            fileInfo.IsReadOnly = false;
        }

        using FileStream fileStream = fileInfo.Create();
        JsonWriterOptions options = new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            IndentCharacter = ' ',
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

#if !USELIGHTWEIGHTJSONPARSER
        using Utf8JsonWriter writer = new Utf8JsonWriter(fileStream, options);

        CurrentGameConfigNode.WriteTo(writer);
#else
        HBRGameLauncherConfig.SerializeToStreamAsync(CurrentGameConfig, fileStream, options: options).Wait();
#endif
        SharedStatic.InstanceLogger.LogTrace("[HBRGameManager::SaveConfig] Saved game-launcher-config.json to directory: {Dir}", CurrentGameInstallPath);
    }
}
