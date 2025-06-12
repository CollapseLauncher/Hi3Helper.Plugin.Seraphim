using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.HBR.Utility;

// ReSharper disable once InconsistentNaming
internal class HBRUlongToBytesJsonConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String when ulong.TryParse(reader.ValueSpan, null, out ulong result) =>
                BitConverter.GetBytes(result),
            JsonTokenType.Number when reader.TryGetUInt64(out ulong value) => BitConverter.GetBytes(value),
            _ => throw new InvalidOperationException("Value is not a number or even a number in a string!")
        };

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}
