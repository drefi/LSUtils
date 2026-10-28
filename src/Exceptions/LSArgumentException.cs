namespace LSUtils;

public class LSArgumentException : LSException {
    public LSArgumentException() { }
    public LSArgumentException(string? paramName) : base(paramName) { }
    public LSArgumentException(string msg, LSException innerException) : base(msg, innerException) { }
    public LSArgumentException(string paramName, string message) : base(message) {
        ParamName = paramName;
    }
    public string? ParamName { get; }

    public static void ThrowIfNull(object? argument, string? paramName = null) {
        if (argument is null) throw new LSArgumentNullException(paramName);
    }
    public static void ThrowIfOutOfRange<T>(System.Func<T, bool> predicate, T argument, string? paramName = null) where T : notnull {
        if (predicate is null) throw new LSArgumentNullException(nameof(predicate));
        if (!predicate.Invoke(argument)) throw new LSArgumentOutOfRangeException(paramName);
    }
}
