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

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.PresetConfig;

[GeneratedComClass]
public partial class HBRGlobalPresetConfig : PluginPresetConfigBase
{
    private const string AuthenticationSalt1 = "";
    private const string AuthenticationSalt2 = "DE7108E9B2842FD460F4777702727869";

    private static readonly List<string> _supportedLanguages = ["Japanese", "English"];

    [field: AllowNull, MaybeNull]
    public override string GameName => field ??= "Heaven Burns Red";

    [field: AllowNull, MaybeNull]
    public override string GameExecutableName => field ??= "HeavenBurnsRed.exe";

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
    public override string ZonePosterUrl => field ??= "https://launcher-pkg-hbr-en.yo-star.com/prod/HBR_EN/launcher_background_img/998105fcbe05f3a9202906dc4f0a43ae.png";

    [field: AllowNull, MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://heavenburnsred.yo-star.com/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.ClosedBeta;

    [field: AllowNull, MaybeNull]
    public override string GameMainLanguage => field ??= "en";

    [field: AllowNull, MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "HeavenBurnsRed";

    public override List<string> SupportedLanguages => _supportedLanguages;

    public override ILauncherApiMedia? LauncherApiMedia { get; } = new HBRGlobalLauncherApiMedia("https://api-launcher-en.yo-star.com/", "HBR_EN", AuthenticationSalt1, AuthenticationSalt2);

    public override ILauncherApiNews? LauncherApiNews { get; } = new HBRGlobalLauncherApiNews("https://api-launcher-en.yo-star.com/", "HBR_EN", AuthenticationSalt1, AuthenticationSalt2);

    public override IGameManager GameManager { get; } 

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}
