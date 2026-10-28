namespace LSUtils;

public class LSArgumentOutOfRangeException : LSArgumentException {
    public LSArgumentOutOfRangeException() { }
    public LSArgumentOutOfRangeException(string? paramName) : base(paramName) { }
    public LSArgumentOutOfRangeException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentOutOfRangeException(string paramName, string message) : base(paramName, message) { }
}
