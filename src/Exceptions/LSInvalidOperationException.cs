namespace LSUtils;

public class LSInvalidOperationException : LSException {
    public LSInvalidOperationException() { }
    public LSInvalidOperationException(string message) : base(message) { }
    public LSInvalidOperationException(string message, LSException innerException) : base(message, innerException) { }
}
