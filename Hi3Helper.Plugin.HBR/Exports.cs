using Hi3Helper.Plugin.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Hi3Helper.Plugin.HBR
{
    public static class Exports
    {
        private static readonly HBRPlugin ThisPluginInstance = new();

        [UnmanagedCallersOnly(EntryPoint = "GetPluginVersion", CallConvs = [typeof(CallConvCdecl)])]
        public static int GetPluginVersion() => 1;

        [UnmanagedCallersOnly(EntryPoint = "GetPlugin", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe void* GetPlugin() =>
            ComInterfaceMarshaller<IPlugin>.ConvertToUnmanaged(ThisPluginInstance);
    }
}
