namespace LSUtils.EventSystem;

/// <summary>
/// Result of phase execution in v4.
/// Controls transition between phases.
/// </summary>
public enum PhaseProcessResult {
    UNKNOWN,
    /// <summary>
    /// All handlers completed with success, can continue to next phase.
    /// </summary>
    CONTINUE,

    FAILURE,

    /// <summary>
    /// Phase needs an external action to continue.
    /// Handler may need proper phase context to control BUSINESS state (to resume or abort).
    /// No async/threads - external actor must resume.
    /// </summary>
    WAITING,

    /// <summary>
    /// At least one handler was cancelled, phase ends immediately.
    /// Transitions Business state to Cancelled state.
    /// </summary>
    CANCELLED,

    COMPLETED,
}
