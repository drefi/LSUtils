namespace LSUtils.EventSystem;

/// <summary>
/// Result of handler execution in v4.
/// Controls flow within phases and phase transitions.
/// </summary>
public enum HandlerProcessResult {
    UNKNOWN,
    /// <summary>
    /// Handler finished successfully, continue to next handler.
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Handler failed, skip remaining handlers and continue to next phase.
    /// </summary>
    FAILURE,
    
    /// <summary>
    /// Will immediately end the current phase and transition to CANCELLED state.
    /// </summary>
    CANCELLED,
    
    /// <summary>
    /// This will halt the phase execution until an external actor resumes or aborts the BUSINESS state.
    /// No async/threads - external control required.
    /// </summary>
    WAITING,
}
