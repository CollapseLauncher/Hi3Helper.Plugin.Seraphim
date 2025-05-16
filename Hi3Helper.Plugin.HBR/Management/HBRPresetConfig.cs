using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using System.Collections.Generic;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

[GeneratedComClass]
public partial class HBRPresetConfig : PluginPresetConfigBase
{
    private static List<string> _supportedLanguages => ["Japanese", "English"];

    public override string GameName => "Heaven Burns Red";
    public override string GameExecutableName => "HeavenBurnsRed.exe";
    public override string ProfileName => "HBRGlobal";
    public override string ZoneDescription =>
        "Heaven Burns Red is a story-driven role-playing game co-developed by WRIGHT FLYER STUDIOS and VISUAL ARTS/Key. " +
        "This title is Jun Maeda's first completely new game in 15 years. " +
        "As a narrative-centric RPG, Heaven Burns Red uses a timeline narrative design to advance the story, " +
        "breaking new ground in the RPG genre by offering an engaging and evolving storyline driven by player choices.";
    public override string ZoneName => "Global";
    public override string ZoneFullName => "Heaven Burns Red (Global)";
    public override string ZoneLogoUrl => string.Empty;
    public override string ZonePosterUrl => string.Empty;
    public override string ZoneHomePageUrl => "https://heavenburnsred.yo-star.com/";
    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;
    public override string GameMainLanguage => "en";
    public override string LauncherGameDirectoryName => "HeavenBurnsRed";
    public override List<string> SupportedLanguages => _supportedLanguages;
}
