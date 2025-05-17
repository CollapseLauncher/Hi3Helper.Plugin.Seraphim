using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.HBR.Management.Api;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.PresetConfig;

[GeneratedComClass]
public partial class HBRGlobalPresetConfig : PluginPresetConfigBase
{
    private static readonly List<string> _supportedLanguages = ["Japanese", "English"];
    private static readonly ILauncherApiMedia _launcherApiMedia = new HBRGlobalLauncherApiMedia();

    [field: AllowNull, MaybeNull]
    public override string GameName => field ??= "Heaven Burns Red";
    [field: AllowNull, MaybeNull]
    public override string GameExecutableName => field ??= "HeavenBurnsRed.exe";
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
    public override string ZoneLogoUrl => field ??= string.Empty;
    [field: AllowNull, MaybeNull]
    public override string ZonePosterUrl => field ??= string.Empty;
    [field: AllowNull, MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://heavenburnsred.yo-star.com/";
    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;
    [field: AllowNull, MaybeNull]
    public override string GameMainLanguage => field ??= "en";
    [field: AllowNull, MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "HeavenBurnsRed";
    public override List<string> SupportedLanguages => _supportedLanguages;
    public override ILauncherApiMedia LauncherApiMedia => _launcherApiMedia;
}
