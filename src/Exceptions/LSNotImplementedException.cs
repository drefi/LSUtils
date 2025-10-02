namespace LSUtils;

public class LSNotImplementedException : LSException {
    public LSNotImplementedException() { }
    public LSNotImplementedException(string msg) : base(msg) { }
    public LSNotImplementedException(string message, LSException innerException) : base(message, innerException) { }
}
