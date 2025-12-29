namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class LSSerializerInfoConverter : JsonConverter<LSSerializerInfo> {
    public LSSerializerInfoConverter() {
    }

    public override LSSerializerInfo Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        return LSSerializerInfo.FromString(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, LSSerializerInfo value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }
}
