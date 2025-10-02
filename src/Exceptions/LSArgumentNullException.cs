namespace LSUtils;

public class LSArgumentNullException : LSException {
    public LSArgumentNullException() { }
    public LSArgumentNullException(string paramName) : base(paramName) { }
    public LSArgumentNullException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentNullException(string paramName, string message) : base(message) {
        ParamName = paramName;
    }
    public string? ParamName { get; }
}
