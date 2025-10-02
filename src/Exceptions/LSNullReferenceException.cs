namespace LSUtils;

public class LSNullReferenceException : LSException {
    public LSNullReferenceException() { }
    public LSNullReferenceException(string msg) : base(msg) { }
    public LSNullReferenceException(string message, LSException innerException) : base(message, innerException) { }
}
