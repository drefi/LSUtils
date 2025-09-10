namespace LSUtils.EventSystem;

/// <summary>
/// Result of handler execution in v4.
/// 
/// Controls flow within phases and phase transitions by indicating how a handler
/// completed its execution. Each result type triggers specific behavior in the
/// phase processing logic.
/// 
/// Handler results are evaluated by the phase state to determine:
/// - Whether to continue to the next handler
/// - Whether to pause processing for external input
/// - Whether to transition to a different state
/// - Whether to terminate processing immediately
/// 
/// Usage Guidelines:
/// - Return SUCCESS for normal completion
/// - Return FAILURE for recoverable errors that don't stop other handlers
/// - Return CANCELLED for critical errors requiring immediate termination
/// - Return WAITING for operations requiring external input
/// - Never return UNKNOWN from handler code (used internally for error conditions)
/// </summary>
public enum HandlerProcessResult {
    /// <summary>
    /// Unknown or uninitialized state.
    /// Used internally for error tracking and should never be returned by handler code.
    /// Indicates an unexpected condition in handler execution.
    /// </summary>
    UNKNOWN,
    
    /// <summary>
    /// Handler finished successfully, continue to next handler.
    /// 
    /// Indicates normal successful completion of handler logic.
    /// Processing continues with the next handler in priority order
    /// within the current phase, or proceeds to the next phase if
    /// this was the last handler.
    /// 
    /// Use Case: Normal business logic completion
    /// Example: Validation passed, data processed successfully, resource allocated
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Handler failed, but processing can continue with other handlers.
    /// 
    /// Indicates a recoverable failure that doesn't prevent other handlers
    /// from executing. The phase will continue processing remaining handlers
    /// but will be marked as having failures. This affects final phase outcome
    /// and may trigger failure handling logic.
    /// 
    /// Use Case: Non-critical failures, validation errors, recoverable exceptions
    /// Example: Optional validation failed, external service unavailable, data format issues
    /// </summary>
    FAILURE,
    
    /// <summary>
    /// Critical failure - immediately end the current phase and transition to CANCELLED state.
    /// 
    /// Indicates a critical error that requires immediate termination of processing.
    /// No further handlers in the current phase will execute, and the event will
    /// transition to the CancelledState for cleanup and finalization.
    /// 
    /// Use Case: Security violations, critical validation failures, system errors
    /// Example: Authentication failed, insufficient permissions, corrupt data detected
    /// </summary>
    CANCELLED,
    
    /// <summary>
    /// Handler requires external input - halt phase execution until resumed or aborted.
    /// 
    /// Indicates that the handler has initiated an asynchronous operation and needs
    /// to wait for external input before continuing. The phase execution pauses,
    /// and external actors must call Resume(), Cancel(), or Fail() on the context
    /// to continue processing.
    /// 
    /// Important Notes:
    /// - No internal threading is used - external control is required
    /// - Handler should set up callbacks to resume processing
    /// - Event data can be used to track the waiting operation
    /// - Timeouts should be handled by external systems
    /// 
    /// Use Case: Async operations, external service calls, user input required
    /// Example: Email verification, payment processing, file upload, external API calls
    /// </summary>
    WAITING,
}
