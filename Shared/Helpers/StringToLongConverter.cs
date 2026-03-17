using System.Text.Json;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Helpers;


public class StringToLongConverter : JsonConverter<long> {
    public override bool CanConvert(Type t) => t == typeof(long);

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.GetString();
        long l;
        if (value != null && long.TryParse(value, out l)) {
            return l;
        }

        throw new Exception("Cannot unmarshal type long");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }

    public static readonly StringToLongConverter Singleton = new();
}