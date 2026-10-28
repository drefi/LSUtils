namespace LSUtils;

public class LSArgumentNullException : LSArgumentException {
    public LSArgumentNullException() { }
    public LSArgumentNullException(string? paramName) : base(paramName) { }
    public LSArgumentNullException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentNullException(string paramName, string message) : base(paramName, message) { }
}
