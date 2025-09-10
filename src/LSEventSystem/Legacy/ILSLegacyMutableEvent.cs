namespace LSUtils.EventSystem;

/// <summary>
/// Internal interface that extends ILSEvent with mutable state properties for event processing.
/// This interface is used exclusively by the event dispatcher and related infrastructure
/// to manage event state during processing phases.
/// 
/// User code should never implement this interface directly - it's an implementation detail
/// of the event processing pipeline that maintains clean separation between immutable
/// event data (accessible to handlers) and mutable processing state (managed by the system).
/// </summary>
internal interface ILSLegacyMutableEvent : ILSEvent {
    /// <summary>
    /// Gets or sets whether the event processing was cancelled.
    /// When set to true, the event dispatcher will halt further phase processing.
    /// </summary>
    new bool IsCancelled { get; set; }

    /// <summary>
    /// Gets or sets whether the event has failures but processing can continue.
    /// When set to true, the event will proceed to the FAILURE phase instead of SUCCESS phase.
    /// </summary>
    new bool HasFailures { get; set; }

    /// <summary>
    /// Gets or sets whether the event has completed processing successfully.
    /// Set to true when all registered phases have been executed without cancellation.
    /// </summary>
    new bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets whether the event is currently waiting for an async operation to complete.
    /// When true, the dispatcher pauses processing until Resume(), Abort(), or Fail() is called.
    /// Automatically set when a handler returns LSPhaseResult.WAITING.
    /// </summary>
    bool IsWaiting { get; set; }
}
