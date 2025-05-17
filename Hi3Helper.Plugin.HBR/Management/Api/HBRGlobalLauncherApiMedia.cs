using Hi3Helper.Plugin.Core.Management.Api;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;
using System.Threading;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiMedia : LauncherApiMediaBase, ILauncherApiMedia
{
    public override LauncherBackgroundFlag GetBackgroundFlag()
        => LauncherBackgroundFlag.IsSourceFile | LauncherBackgroundFlag.TypeIsImage;

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}
