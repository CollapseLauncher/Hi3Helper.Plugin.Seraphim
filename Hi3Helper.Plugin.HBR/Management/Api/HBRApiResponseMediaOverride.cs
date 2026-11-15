using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Hi3Helper.Plugin.Core;
using Microsoft.Extensions.Logging;

#if USELIGHTWEIGHTJSONPARSER
using Hi3Helper.Plugin.Core.Utility.Json;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

public class HBRApiResponseMediaOverrideEntry
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<HBRApiResponseMediaOverrideEntry>
#endif
{
    public string?        BackgroundUrl { get; set; }
    public DateTimeOffset ExpireDate    { get; init; }

    public static List<HBRApiResponseMediaOverrideEntry>? ParseListFrom(JsonElement element)
    {
        try
        {
            List<HBRApiResponseMediaOverrideEntry> entries = [];
            foreach (JsonElement entry in element.EnumerateArray())
            {
                HBRApiResponseMediaOverrideEntry? parsedEntry = ParseFrom(entry);
                if (parsedEntry == null)
                    continue;

                entries.Add(parsedEntry);
            }

            return entries.Count == 0 ? null : entries;
        }
        catch (Exception e)
        {
            SharedStatic.InstanceLogger.LogError(e, "Cannot parse HBRApiResponseMediaOverrideEntry due to an error");
            return null;
        }
    }

    public static HBRApiResponseMediaOverrideEntry? ParseFrom(JsonElement element)
    {
        Unsafe.SkipInit(out JsonElement expireDateProp);

        if (!element.TryGetProperty(nameof(BackgroundUrl), out JsonElement backgroundElementProp) ||
            backgroundElementProp.GetString() is not { } backgroundElement)
        {
            return null;
        }

        bool hasExpireDate = element.TryGetProperty(nameof(ExpireDate), out expireDateProp);

        return new HBRApiResponseMediaOverrideEntry
        {
            BackgroundUrl = backgroundElement,
            ExpireDate    = hasExpireDate ? expireDateProp.GetDateTimeOffset() : default
        };
    }
}
