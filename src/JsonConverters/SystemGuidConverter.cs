namespace LSUtils;

using System.Text.Json;
using System.Text.Json.Serialization;

public class SystemGuidConverter : JsonConverter<System.Guid> {
    public SystemGuidConverter() {
    }

    public override System.Guid Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        return System.Guid.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, System.Guid value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }
}
