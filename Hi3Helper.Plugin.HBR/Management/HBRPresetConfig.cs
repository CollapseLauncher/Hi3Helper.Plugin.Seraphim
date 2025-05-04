using Hi3Helper.Plugin.Core.Management;
using System.Runtime.InteropServices.Marshalling;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

[GeneratedComClass]
public partial class HBRPresetConfig : IPluginPresetConfig
{
    private static string[] SupportedLanguages => ["Japanese", "English"];

    public string get_GameName() => "Heaven Burns Red";
    public string get_ProfileName() => "HBRGlobal";
    public string get_ZoneDescription() =>
        "Heaven Burns Red is a story-driven role-playing game co-developed by WRIGHT FLYER STUDIOS and VISUAL ARTS/Key. " +
        "This title is Jun Maeda's first completely new game in 15 years. " +
        "As a narrative-centric RPG, Heaven Burns Red uses a timeline narrative design to advance the story, " +
        "breaking new ground in the RPG genre by offering an engaging and evolving storyline driven by player choices.";
    public string get_ZoneName() => "Global";
    public string get_ZoneFullName() => "Heaven Burns Red (Global)";
    public string get_ZoneLogoUrl() => string.Empty;
    public string get_ZonePosterUrl() => string.Empty;
    public string get_ZoneHomePageUrl() => "https://heavenburnsred.yo-star.com/";
    public GameReleaseChannel get_ReleaseChannel() => GameReleaseChannel.Public;
    public string get_GameMainLanguage() => "en";
    public string get_GameSupportedLanguages(int index)
    {
        if (index >= SupportedLanguages.Length || index < 0)
            return string.Empty;

        return SupportedLanguages[index];
    }
    public int get_GameSupportedLanguagesCount() => SupportedLanguages.Length;
    public string get_GameExecutableName() => "HeavenBurnsRed.exe";
    public string get_LauncherGameDirectoryName() => "HeavenBurnsRed";
}
