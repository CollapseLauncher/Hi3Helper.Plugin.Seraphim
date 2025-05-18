using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Hi3Helper.Plugin.HBR
{
    public class Exports : SharedStatic
    {
        private static GameVersion _thisPluginVersion = new(0, 0, 1, 0);

        [UnmanagedCallersOnly(EntryPoint = "GetPluginStandardVersion", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe GameVersion* GetPluginStandardVersion() => (GameVersion*)Unsafe.AsPointer(ref LibraryStandardVersion);

        [UnmanagedCallersOnly(EntryPoint = "GetPluginVersion", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe GameVersion* GetPluginVersion() => (GameVersion*)Unsafe.AsPointer(ref _thisPluginVersion);

        [UnmanagedCallersOnly(EntryPoint = "GetPlugin", CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe void* GetPlugin() =>
            ComInterfaceMarshaller<IPlugin>.ConvertToUnmanaged(ThisPluginInstance ??= new HBRPlugin());

        [UnmanagedCallersOnly(EntryPoint = "SetLoggerCallback", CallConvs = [typeof(CallConvCdecl)])]
        public static void SetLoggerCallback(nint loggerCallback)
        {
            if (loggerCallback == nint.Zero)
            {
                InstanceLogger?.LogTrace("[Exports::SetLoggerCallback] Logger callback has been detached!");
                InstanceLoggerCallback = null;
                return;
            }

            InstanceLogger?.LogTrace("[Exports::SetLoggerCallback] Logger callback has been attached to address: 0x{Ptr:x8}", loggerCallback);
            InstanceLoggerCallback = Marshal.GetDelegateForFunctionPointer<SharedLoggerCallback>(loggerCallback);
        }

        [UnmanagedCallersOnly(EntryPoint = "SetDnsResolverCallback", CallConvs = [typeof(CallConvCdecl)])]
        public static void SetDnsResolverCallback(nint dnsResolverCallback)
        {
            if (dnsResolverCallback == nint.Zero)
            {
                InstanceLogger?.LogTrace("[Exports::SetDnsResolverCallback] DNS Resolver callback has been detached!");
                InstanceDnsResolverCallback = null;
                return;
            }

            InstanceLogger?.LogTrace("[Exports::SetDnsResolverCallback] DNS Resolver callback has been attached to address: 0x{Ptr:x8}", dnsResolverCallback);
            InstanceDnsResolverCallback = Marshal.GetDelegateForFunctionPointer<SharedDnsResolverCallback>(dnsResolverCallback);
        }
    }
}
