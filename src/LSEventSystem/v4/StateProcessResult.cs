namespace LSUtils.EventSystem;

public enum StateProcessResult {
    UNKNOWN,
    CONTINUE, //all handlers executed successfully;
    FAILURE, //failure occurred go to [completed state];
    WAITING, //event is waiting for external input;
    CANCELLED, //event was cancelled go to [cancelled state];
}
