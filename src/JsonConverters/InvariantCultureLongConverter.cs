namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class InvariantCultureLongConverter : JsonConverter<long> {
    public override long Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            var stringValue = reader.GetString();
            if (long.TryParse(stringValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }
        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
