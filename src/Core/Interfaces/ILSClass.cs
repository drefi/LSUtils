namespace LSUtils;

public interface ILSClass {
    /// <summary>
    /// Gets the class name of the eventable.
    /// </summary>
    static virtual string ClassName { get; } = typeof(ILSClass).AssemblyQualifiedName ?? string.Empty;
    const string CLASS_NAME_LABEL = "class_name";
}
