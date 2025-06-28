using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Utility;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class HBRIconDataMap : JsonSerializerContext;
#endif

public static partial class HBRIconData
{
    private static Dictionary<string, string>? _hbrIconDataMapDictionary;

    internal static Dictionary<string, byte[]> EmbeddedDataDictionary = new(StringComparer.OrdinalIgnoreCase);

    public static async Task Initialize(CancellationToken token)
    {
        if (EmbeddedDataDictionary.Count == 0)
        {
            await LoadEmbeddedData(token);
        }
    }

    public static byte[]? GetEmbeddedData(string key)
        => EmbeddedDataDictionary.GetValueOrDefault(key);

#if USELIGHTWEIGHTJSONPARSER
    private static async Task<Dictionary<string, string>> GetDictionaryAsync(Stream stream, CancellationToken token)
    {
        Dictionary<string, string> ret = [];

        JsonDocument document = await JsonDocument.ParseAsync(stream, default, token);
        foreach (var keyValue in document.RootElement.EnumerateObject())
        {
            string key = keyValue.Name;
            string value = keyValue.Value.GetString() ?? "";

            ret.Add(key, value);
        }

        return ret;
    }
#endif

    private static async Task LoadEmbeddedData(CancellationToken token)
    {
        await using Stream    base64EmbeddedData = GetEmbeddedStream();
        await using TarReader tarReader          = new(base64EmbeddedData);
        while (await tarReader.GetNextEntryAsync(true, token) is { } entry)
        {
            await using Stream? copyToStream = entry.DataStream;
            if (copyToStream == null)
            {
                continue;
            }

            string entryName = entry.Name;
            if (entryName.EndsWith("Map.json", StringComparison.OrdinalIgnoreCase))
            {
                _hbrIconDataMapDictionary =
#if USELIGHTWEIGHTJSONPARSER
                    await GetDictionaryAsync(copyToStream, token);
#else
                    await JsonSerializer.DeserializeAsync(copyToStream, HBRIconDataMap.Default.DictionaryStringString, token);
#endif
                if (_hbrIconDataMapDictionary == null)
                {
                    throw new NullReferenceException("Cannot initialize MediaIconMap.json inside of the EmbeddedData");
                }

                Dictionary<string, string> keyValueReversed = _hbrIconDataMapDictionary.ToDictionary();
                _hbrIconDataMapDictionary.Clear();
                foreach (KeyValuePair<string, string> a in keyValueReversed)
                {
                    _hbrIconDataMapDictionary.Add(a.Value, a.Key);
                }

                continue;
            }

            byte[] data;
            if (copyToStream is MemoryStream memoryStream)
            {
                data = await Task.Factory.StartNew(memoryStream.ToArray, token).ConfigureAwait(false);
            }
            else
            {
                data = new byte[entry.Length];
                await copyToStream.ReadAtLeastAsync(data, data.Length, false, token).ConfigureAwait(false);
            }

            string key = Path.GetFileNameWithoutExtension(entryName);
            if (!_hbrIconDataMapDictionary!.TryGetValue(key, out string? keyAsValue) || string.IsNullOrEmpty(keyAsValue))
            {
                continue;
            }
            _ = EmbeddedDataDictionary.TryAdd(keyAsValue, data);
        }
    }

    private static unsafe BrotliStream GetEmbeddedStream()
    {
        fixed (byte* stringAddress = &EmbeddedData[0])
        {
            UnmanagedMemoryStream stream = new(stringAddress, EmbeddedData.Length * 2);
            BrotliStream brotliDecompressStream = new(stream, CompressionMode.Decompress);
            return brotliDecompressStream;
        }
    }
}
