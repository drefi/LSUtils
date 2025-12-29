namespace LSUtils;

public interface ILSSerializable : ILSClass {
    /// <summary>
    /// Saves the object to a string using the provided serializer.
    /// </summary>
    /// <param name="serializer">The serializer to use for saving.</param>
    /// <returns>A string representation of the saved object.</returns>
    string Save(ILSSerializer serializer);
}
