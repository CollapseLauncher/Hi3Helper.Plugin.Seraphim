using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.HBR.Management;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable InconsistentNaming
namespace Hi3Helper.Plugin.HBR;

[GeneratedComClass]
public partial class HBRPlugin : IPlugin
{
    private static readonly IPluginPresetConfig[] PresetConfigInstances = [ new HBRPresetConfig() ];
    private static DateTime _pluginCreationDate = new(2025, 05, 04, 09, 15, 0, DateTimeKind.Utc);

    public string GetPluginName() => "Heaven Burns Red Plugin";

    public string GetPluginDescription() => "A plugin for Heaven Burns Red on Collapse Launcher";

    public string GetPluginAuthor() => "neon-nyan, Collapse Project Team";

    public unsafe DateTime* GetPluginCreationDate() => (DateTime*)Unsafe.AsPointer(ref _pluginCreationDate);

    public int GetPresetConfigCount() => PresetConfigInstances.Length;

    public IPluginPresetConfig GetPresetConfig(int index)
    {
        // Avoid crash by returning null if index is out of bounds
        if (index < 0 || index >= PresetConfigInstances.Length)
        {
            return null!;
        }

        // Return preset config at index (n)
        return PresetConfigInstances[index];
    }
}
