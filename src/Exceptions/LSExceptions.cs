namespace LSUtils;
public class LSException : System.Exception {
    public LSException() { }
    public LSException(string msg) : base(msg) { }
    public LSException(string message, LSException innerException) : base(message, innerException) { }
}
