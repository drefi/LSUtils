namespace LSUtils;

public class LSArgumentException : LSException {
    public LSArgumentException() { }
    public LSArgumentException(string paramName) : base(paramName) { }
    public LSArgumentException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentException(string paramName, string message) : base(message) {
        ParamName = paramName;
    }
    public string? ParamName { get; }
}
