namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class InvariantCultureDoubleConverter : JsonConverter<double> {
    public override double Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            var stringValue = reader.GetString();
            if (double.TryParse(stringValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }
        return reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
