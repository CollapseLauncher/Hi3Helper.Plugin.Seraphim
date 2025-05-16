using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Hi3Helper.Plugin.HBR
{
    public class Exports : SharedStatic
    {
        private static readonly HBRPlugin   ThisPluginInstance = new();
        private static          GameVersion _thisPluginVersion = new(0, 0, 1, 0);

        [UnmanagedCallersOnly(EntryPoint = "GetPluginStandardVersion", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe GameVersion* GetPluginStandardVersion() => (GameVersion*)Unsafe.AsPointer(ref LibraryStandardVersion);

        [UnmanagedCallersOnly(EntryPoint = "GetPluginVersion", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe GameVersion* GetPluginVersion() => (GameVersion*)Unsafe.AsPointer(ref _thisPluginVersion);

        [UnmanagedCallersOnly(EntryPoint = "GetPlugin", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe void* GetPlugin() =>
            ComInterfaceMarshaller<IPlugin>.ConvertToUnmanaged(ThisPluginInstance);

        [UnmanagedCallersOnly(EntryPoint = "SetLoggerCallback", CallConvs = [typeof(CallConvCdecl)])]
        public static void SetLoggerCallback(nint loggerCallback)
        {
            if (loggerCallback == nint.Zero)
            {
                InstanceLoggerCallback = null;
                return;
            }

            InstanceLoggerCallback = Marshal.GetDelegateForFunctionPointer<SharedLoggerCallback>(loggerCallback);
        }
    }
}
