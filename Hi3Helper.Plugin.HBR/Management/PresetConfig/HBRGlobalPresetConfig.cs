using System;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.HBR.Management.Api;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.PresetConfig;

[GeneratedComClass]
public partial class HBRGlobalPresetConfig : PluginPresetConfigBase
{
    private const string ApiResponseUrl      = "https://api-launcher-en.yo-star.com/";
    private const string AuthenticationSalt1 = "";
    private const string AuthenticationSalt2 = "DE7108E9B2842FD460F4777702727869";
    private const string CurrentUninstKey    = "8873065a-4511-50bb-94ed-24aee5a854b1";
    private const string CurrentTag          = "HBR_EN";
    private const string ExecutableName      = "HeavenBurnsRed.exe";
    private const string VendorName          = "Yostar";

    private static readonly List<string> _supportedLanguages = ["Japanese", "English"];

    public override string? GameName => field ??= "Heaven Burns Red";

    [field: AllowNull, MaybeNull]
    public override string GameExecutableName => field ??= ExecutableName;

    [field: AllowNull, MaybeNull]
    public override string GameAppDataPath => field ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData",
        "LocalLow",
        // ReSharper disable once StringLiteralTypo
        "yostar",
        "HeavenBurnsRed"
        );

    [field: AllowNull, MaybeNull]
    public override string GameLogFileName => field ??= "Player.log";

    [field: AllowNull, MaybeNull]
    public override string GameRegistryKeyName => field ??= Path.GetFileNameWithoutExtension(ExecutableName);
    
    [field: AllowNull, MaybeNull]
    public override string GameVendorName => field ??= VendorName.ToLower();

    [field: AllowNull, MaybeNull]
    public override string ProfileName => field ??= "HBRGlobal";

    [field: AllowNull, MaybeNull]
    public override string ZoneDescription => field ??=
        "Heaven Burns Red is a story-driven role-playing game co-developed by WRIGHT FLYER STUDIOS and VISUAL ARTS/Key. " +
        "This title is Jun Maeda's first completely new game in 15 years. " +
        "As a narrative-centric RPG, Heaven Burns Red uses a timeline narrative design to advance the story, " +
        "breaking new ground in the RPG genre by offering an engaging and evolving storyline driven by player choices.";

    [field: AllowNull, MaybeNull]
    public override string ZoneName => field ??= "Global";

    [field: AllowNull, MaybeNull]
    public override string ZoneFullName => field ??= "Heaven Burns Red (Global)";

    [field: AllowNull, MaybeNull]
    public override string ZoneLogoUrl => field ??= "https://cdn2.steamgriddb.com/logo_thumb/dae6042416a1c9e5ffbb1d51e9dab7d0.png";

    [field: AllowNull, MaybeNull]
    public override string ZonePosterUrl => field ??= "https://raw.githubusercontent.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/refs/heads/main/metadata/game_posters/poster_hbrtest.png";

    [field: AllowNull, MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://heavenburnsred.yo-star.com/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.ClosedBeta;

    [field: AllowNull, MaybeNull]
    public override string GameMainLanguage => field ??= "en";

    [field: AllowNull, MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "HeavenBurnsRed";

    public override List<string> SupportedLanguages => _supportedLanguages;

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new HBRGlobalLauncherApiMedia(ApiResponseUrl, CurrentTag, AuthenticationSalt1, AuthenticationSalt2);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new HBRGlobalLauncherApiNews(ApiResponseUrl, CurrentTag, AuthenticationSalt1, AuthenticationSalt2);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new HBRGameManager(ExecutableName, ApiResponseUrl, CurrentTag, AuthenticationSalt1, AuthenticationSalt2, CurrentUninstKey);
        set;
    }

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new HBRGameInstaller(GameManager);
        set;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}
