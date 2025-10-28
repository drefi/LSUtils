namespace LSUtils;

using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

public class LSSerializerInfoListConverter : JsonConverter<List<LSSerializerInfo>> {
    public override List<LSSerializerInfo> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) {
        var stringList = JsonSerializer.Deserialize<List<string>>(ref reader, options);
        if (stringList == null) throw new System.Exception("Deserialization of List<LSSerializerInfo> returned null.");

        var infoList = new List<LSSerializerInfo>();
        foreach (var str in stringList) {
            infoList.Add(LSSerializerInfo.FromString(str));
        }
        return infoList;
    }

    public override void Write(Utf8JsonWriter writer, List<LSSerializerInfo> value, JsonSerializerOptions options) {
        var serializedList = value.Select(item => item.ToString()).ToList();
        JsonSerializer.Serialize(writer, serializedList, options);
    }
}
