using Hi3Helper.Plugin.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hi3Helper.Plugin.HBR;

/// <summary>
/// Provides necessary unmanaged API exports for the plugin to be loaded.
/// </summary>
public class Exports : SharedStatic
{
    static Exports() => Load<HBRPlugin>(!RuntimeFeature.IsDynamicCodeCompiled ? new Core.Management.GameVersion(0, 8, 1, 0) : default); // Loads the IPlugin instance as HBRPlugin.

    [UnmanagedCallersOnly(EntryPoint = "TryGetApiExport", CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe int TryGetApiExport(char* exportName, void** delegateP) =>
        TryGetApiExportPointer(exportName, delegateP);
}