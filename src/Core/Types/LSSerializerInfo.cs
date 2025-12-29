namespace LSUtils;

public struct LSSerializerInfo {
    public string Key { get; }
    public string Value { get; }
    public string AssemblyName { get; }
    public object? TypedValue { get; set; }
    public LSSerializerInfo(string key, object value, ILSSerializer serializer) {
        Key = key;
        if (value is System.Type typeValue) {
            Value = typeValue.AssemblyQualifiedName ?? string.Empty;
            AssemblyName = typeof(System.Type).AssemblyQualifiedName ?? string.Empty;
            TypedValue = value;
        } else {
            Value = serializer.Serialize(value, value.GetType());
            TypedValue = value;
            AssemblyName = value.GetType().AssemblyQualifiedName ?? string.Empty;
        }
    }
    public LSSerializerInfo(string key, string value, string assemblyName) {
        Key = key;
        Value = value;
        AssemblyName = assemblyName;
    }
    public LSSerializerInfo(string fromString) {
        //expected format: Key:(Value % AssemblyName)
        var keySplit = fromString.Split(new string[] { ":(" }, System.StringSplitOptions.RemoveEmptyEntries);
        if (keySplit.Length != 2) throw new LSException($"Could not parse LSSerializerInfo from string: {fromString}");
        Key = keySplit[0];
        var valueSplit = keySplit[1].TrimEnd(')').Split(new string[] { " % " }, System.StringSplitOptions.RemoveEmptyEntries);
        if (valueSplit.Length != 2) throw new LSException($"Could not parse LSSerializerInfo from string: {fromString}");
        Value = valueSplit[0];
        AssemblyName = valueSplit[1];
        TypedValue = null;
    }
    public bool TryGetTypedValue(ILSSerializer serializer, out object? typeValue) {
        var type = System.Type.GetType(AssemblyName);
        if (type == null) throw new LSException($"Could not find type {AssemblyName} for key {Key} during GetTypedValue.");
        if (type == typeof(System.Type)) {
            typeValue = System.Type.GetType(Value.Trim('"'));
            if (typeValue == null) return false;
            TypedValue = typeValue;
            return true;
        }
        typeValue = serializer.Deserialize(Value, type);
        if (typeValue == null) return false;
        TypedValue = typeValue;
        return true;
    }
    public T GetTypedValue<T>(ILSSerializer serializer) {
        if (TypedValue is T typed) return typed;
        var type = System.Type.GetType(AssemblyName);
        if (type == null) throw new LSException($"Could not find type {AssemblyName} for key {Key} during GetTypedValue.");
        var deserialized = serializer.Deserialize(Value, type);
        if (deserialized is T casted) {
            TypedValue = casted;
            return casted;
        }
        throw new LSException($"Could not cast deserialized value to type {typeof(T).Name} for key {Key} during GetTypedValue.");
    }
    public override string ToString() {
        return $"{Key}:({Value} % {AssemblyName})";
    }
    public static LSSerializerInfo FromString(string str) {
        return new LSSerializerInfo(str);
    }
}
