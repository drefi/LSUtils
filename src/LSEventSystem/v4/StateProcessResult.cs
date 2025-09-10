namespace LSUtils.EventSystem;

public enum StateProcessResult {
    UNKNOWN,
    SUCCESS, //all handlers executed successfully;
    FAILURE, //failure occurred go to [completed state];
    WAITING, //event is waiting for external input;
    CANCELLED, //event was cancelled go to [cancelled state];
}
public enum EventProcessResult {
    UNKNOWN,
    SUCCESS,
    FAILURE,
    CANCELLED,
    WAITING
}
