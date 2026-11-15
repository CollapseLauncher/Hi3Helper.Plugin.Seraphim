using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Utility;
using System;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;

namespace Hi3Helper.Plugin.HBR;

[GeneratedComClass]
// ReSharper disable once InconsistentNaming
internal partial class HBRPluginSelfUpdate : PluginSelfUpdateBase
{
    protected override ReadOnlySpan<string> BaseCdnUrlSpan   => HBRCDNUtility.BaseCdnUrl;
    protected override HttpClient           UpdateHttpClient { get; }

    internal HBRPluginSelfUpdate()
    {
        PluginHttpClientBuilder builder = new();
        builder.AllowRedirections();
        builder.AllowUntrustedCert();
        builder.AllowCookies();

        UpdateHttpClient = builder.Create();
    }
}
