using Hi3Helper.Plugin.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

namespace Hi3Helper.Plugin.HBR;

/// <summary>
/// Provides necessary unmanaged API exports for the plugin to be loaded.<br/><br/>
/// 
/// NOTE FOR DEVELOPERS:<br/>
/// The export class name can be anything you want. In this example, we use "Seraphim" as a name to the weapon used by the characters.
/// </summary>
public class Seraphim : SharedStatic<Seraphim> // 2025-08-18: We use generic version of SharedStatic<T> to add support for game launch API.
                                               //             Though, the devs can still use the old SharedStatic without any compatibility issue.
{
    static Seraphim() => Load<HBRPlugin>(!RuntimeFeature.IsDynamicCodeCompiled ? new Core.Management.GameVersion(0, 8, 1, 1) : default); // Loads the IPlugin instance as HBRPlugin.

    [UnmanagedCallersOnly(EntryPoint = "TryGetApiExport", CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe int TryGetApiExport(char* exportName, void** delegateP) =>
        TryGetApiExportPointer(exportName, delegateP);
}