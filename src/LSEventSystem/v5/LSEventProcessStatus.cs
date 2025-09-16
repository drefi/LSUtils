namespace LSUtils.EventSystem;

public enum LSEventProcessStatus {
    UNKNOWN, // only used for initialization
    SUCCESS, // processed successfully
    FAILURE, // processed with failure
    WAITING, // waiting for an external event to resume processing
    CANCELLED // processing was cancelled
}
