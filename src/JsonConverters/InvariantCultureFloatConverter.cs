namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class InvariantCultureFloatConverter : JsonConverter<float> {
    public override float Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            var stringValue = reader.GetString();
            if (float.TryParse(stringValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }
        return reader.GetSingle();
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
