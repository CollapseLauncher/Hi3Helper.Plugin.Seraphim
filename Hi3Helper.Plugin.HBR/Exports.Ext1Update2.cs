using Hi3Helper.Plugin.Core.DiscordPresence;

namespace Hi3Helper.Plugin.HBR;

public partial class Seraphim
{
    private const ulong  DiscordPresenceId           = 1420376476976681050u;
    private const string DiscordPresenceLargeIconUrl = "https://play-lh.googleusercontent.com/IzdBGRsLy5Cf9NCTd11VTBAGZX6RaOqUglTAgvl5pRRXTDjDxQc1YlWM4vykHwu2rnpOBTo-Pqh8lON2ko5aLQ";

    protected override bool GetCurrentDiscordPresenceInfoCore(
        DiscordPresenceExtension.DiscordPresenceContext context,
        out ulong                                       presenceId,
        out string?                                     largeIconUrl,
        out string?                                     largeIconTooltip,
        out string?                                     smallIconUrl,
        out string?                                     smallIconTooltip)
    {
        presenceId       = DiscordPresenceId;
        largeIconUrl     = DiscordPresenceLargeIconUrl;
        largeIconTooltip = null;
        smallIconUrl     = null;
        smallIconTooltip = null;

        return true;
    }
}
