namespace LSUtils;

public interface ILSSerializer : ILSClass {
    /// <summary>
    /// Serializes the given object to a string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A string representation of the serialized object.</returns>
    string Serialize<T>(T value);
    string Serialize(object value, System.Type inputType);

    /// <summary>
    /// Deserializes a string back into an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize into.</typeparam>
    /// <param name="data">The string data to deserialize.</param>
    /// <returns>An object of type T.</returns>
    T? Deserialize<T>(string data);

    /// <summary>
    /// When deserializing unknown types
    /// </summary>
    object? Deserialize(string data, System.Type type);
}
