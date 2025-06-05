namespace LSUtils;
public class LSException : System.Exception {
    public LSException() { }
    public LSException(string msg) : base(msg) { }
    public LSException(string message, LSException innerException) : base(message, innerException) { }
}
public class LSNotImplementedException : LSException {
    public LSNotImplementedException() { }
    public LSNotImplementedException(string msg) : base(msg) { }
    public LSNotImplementedException(string message, LSException innerException) : base(message, innerException) { }
}
public class LSNullReferenceException : LSException {
    public LSNullReferenceException() { }
    public LSNullReferenceException(string msg) : base(msg) { }
    public LSNullReferenceException(string message, LSException innerException) : base(message, innerException) { }
}
public class LSArgumentNullException : LSException {
    public LSArgumentNullException() { }
    public LSArgumentNullException(string paramName) : base(paramName) { }
    public LSArgumentNullException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentNullException(string paramName, string message) : base(message) {
        ParamName = paramName;
    }
    public string? ParamName { get; }
}
public class LSArgumentException : LSException {
    public LSArgumentException() { }
    public LSArgumentException(string paramName) : base(paramName) { }
    public LSArgumentException(string message, LSException innerException) : base(message, innerException) { }
    public LSArgumentException(string paramName, string message) : base(message) {
        ParamName = paramName;
    }
    public string? ParamName { get; }
}
public class LSNotificationException : LSException {
    public LSNotificationException(string message) : base(message) { }
    public LSNotificationException() { }
}
