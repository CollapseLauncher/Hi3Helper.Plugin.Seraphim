#if USELIGHTWEIGHTJSONPARSER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.HBR.Utility.Json;

public static class JsonUtils
{
    public static async Task<Dictionary<TKey, TValue>?> GetDictFromJsonAsync<TKey, TValue>(this Stream fromStream, bool isDisposeStream = false, CancellationToken token = default)
        where TKey : ISpanParsable<TKey>
        where TValue : ISpanParsable<TValue>
    {
        Dictionary<TKey, TValue> dict = [];

        try
        {
            using JsonDocument jsonDocument = await ParseJsonDocumentFromAsync(fromStream, token: token);
            foreach (var objectEntry in jsonDocument.RootElement.EnumerateObject())
            {
                if (TKey.TryParse(objectEntry.Name, null, out TKey? resultKey) &&
                    resultKey != null &&
                    TValue.TryParse(objectEntry.Value.GetString(), null, out TValue? resultValue))
                {
                    dict.Add(resultKey, resultValue);
                }
            }

            return dict.Count == 0 ? null : dict;
        }
        finally
        {
            if (isDisposeStream)
            {
                await fromStream.DisposeAsync();
            }
        }
    }

    public static async Task<Dictionary<string, TValue>?> GetDictFromJsonAsync<TValue>(this Stream fromStream, bool isDisposeStream = false, CancellationToken token = default)
        where TValue : ISpanParsable<TValue>
    {
        Dictionary<string, TValue> dict = [];

        try
        {
            using JsonDocument jsonDocument = await ParseJsonDocumentFromAsync(fromStream, token: token);
            foreach (var objectEntry in jsonDocument.RootElement.EnumerateObject())
            {
                if (!string.IsNullOrEmpty(objectEntry.Name) &&
                    TValue.TryParse(objectEntry.Value.GetString(), null, out TValue? resultValue))
                {
                    dict.Add(objectEntry.Name, resultValue);
                }
            }

            return dict.Count == 0 ? null : dict;
        }
        finally
        {
            if (isDisposeStream)
            {
                await fromStream.DisposeAsync();
            }
        }
    }

    public static async Task<Dictionary<string, string?>?> GetDictFromJsonAsync(this Stream fromStream, bool isDisposeStream = false, CancellationToken token = default)
    {
        Dictionary<string, string?> dict = [];

        try
        {
            using JsonDocument jsonDocument = await ParseJsonDocumentFromAsync(fromStream, token: token);
            foreach (var objectEntry in jsonDocument.RootElement.EnumerateObject().Where(objectEntry => !string.IsNullOrEmpty(objectEntry.Name)))
            {
                dict.Add(objectEntry.Name, objectEntry.Value.GetString());
            }

            return dict.Count == 0 ? null : dict;
        }
        finally
        {
            if (isDisposeStream)
            {
                await fromStream.DisposeAsync();
            }
        }
    }

    // ReSharper disable once UnusedMember.Local
    private static JsonDocument ParseJsonDocumentFrom<T>(T fromSource, JsonDocumentOptions options = default)
        => fromSource switch
        {
            Stream asStream => JsonDocument.Parse(asStream, options),
            string asString => JsonDocument.Parse(asString, options),
            ReadOnlyMemory<byte> asReadOnlyMemory => JsonDocument.Parse(asReadOnlyMemory, options),
            ReadOnlyMemory<char> asReadOnlyMemoryChar => JsonDocument.Parse(asReadOnlyMemoryChar, options),
            ReadOnlySequence<byte> asReadOnlySequence => JsonDocument.Parse(asReadOnlySequence, options),
            _ => throw new NotSupportedException(
                "Source type isn't supported. Please use Stream or string or ReadOnlyMemory<byte> or ReadOnlyMemory<char> or ReadOnlySequence<byte> as your source.")
        };

    private static Task<JsonDocument> ParseJsonDocumentFromAsync<T>(T fromSource, JsonDocumentOptions options = default, CancellationToken token = default)
        => fromSource switch
        {
            Stream asStream => JsonDocument.ParseAsync(asStream, options, token),
            _ => throw new NotSupportedException(
                "Source type isn't supported. Please use Stream as your source.")
        };
}
#endif
