namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class InvariantCultureIntConverter : JsonConverter<int> {
    public override int Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            var stringValue = reader.GetString();
            if (int.TryParse(stringValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }
        return reader.GetInt32();
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
