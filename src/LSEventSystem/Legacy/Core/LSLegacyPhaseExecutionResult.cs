namespace LSUtils.EventSystem;

/// <summary>
/// Represents the result of processing a phase in the event pipeline.
/// Used internally by the dispatcher to control event flow and handle failures.
/// </summary>
internal struct LSLegacyPhaseExecutionResult {
    /// <summary>
    /// Indicates if the phase executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Indicates if the event should wait for async operations to complete.
    /// </summary>
    public bool ShouldWait { get; init; }

    /// <summary>
    /// Indicates if the event should be cancelled immediately.
    /// </summary>
    public bool ShouldCancel { get; init; }

    /// <summary>
    /// Indicates if the event has failures but processing can continue.
    /// </summary>
    public bool HasFailures { get; init; }

    /// <summary>
    /// Optional error message describing the failure or cancellation reason.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful phase execution result.
    /// </summary>
    /// <returns>A successful phase execution result.</returns>
    public static LSLegacyPhaseExecutionResult Successful() => new() { Success = true };

    /// <summary>
    /// Creates a waiting phase execution result for async operations.
    /// </summary>
    /// <returns>A waiting phase execution result.</returns>
    public static LSLegacyPhaseExecutionResult Waiting() => new() { Success = true, ShouldWait = true };

    /// <summary>
    /// Creates a cancelled phase execution result.
    /// </summary>
    /// <param name="reason">Optional reason for cancellation.</param>
    /// <returns>A cancelled phase execution result.</returns>
    public static LSLegacyPhaseExecutionResult Cancelled(string? reason = null) => 
        new() { ShouldCancel = true, ErrorMessage = reason };

    /// <summary>
    /// Creates a failed phase execution result.
    /// </summary>
    /// <param name="reason">Optional reason for failure.</param>
    /// <returns>A failed phase execution result.</returns>
    public static LSLegacyPhaseExecutionResult Failed(string? reason = null) => 
        new() { HasFailures = true, ErrorMessage = reason };
}
