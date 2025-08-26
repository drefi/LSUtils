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
/// <remarks>
/// This separation ensures that:
/// - Event handlers cannot accidentally corrupt event processing state
/// - Event state mutations are centralized in the dispatcher
/// - The event interface remains clean and focused on data access
/// - Internal processing logic is isolated from public APIs
/// </remarks>
internal interface ILSMutableEvent : ILSEvent {
    /// <summary>
    /// Gets or sets whether the event processing was cancelled.
    /// When set to true, the event dispatcher will halt further phase processing.
    /// This property is managed by the dispatcher based on handler results.
    /// </summary>
    new bool IsCancelled { get; set; }

    /// <summary>
    /// Gets or sets whether the event has completed processing successfully.
    /// Set to true when all registered phases have been executed without cancellation.
    /// This property is managed by the dispatcher at the end of event processing.
    /// </summary>
    new bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the current phase being executed.
    /// Updated by the dispatcher as it progresses through the event processing pipeline.
    /// This provides visibility into the event's current position in the processing flow.
    /// </summary>
    new LSEventPhase CurrentPhase { get; set; }

    /// <summary>
    /// Gets or sets the flags indicating which phases have been completed successfully.
    /// Uses bitwise operations to efficiently track completion state across multiple phases.
    /// Updated by the dispatcher when each phase completes without errors or cancellation.
    /// </summary>
    new LSEventPhase CompletedPhases { get; set; }

    /// <summary>
    /// Gets whether the event is currently waiting for an async operation to complete.
    /// When true, the dispatcher should pause processing until ContinueProcessing() is called.
    /// This property is automatically set when a handler returns LSPhaseResult.WAITING.
    /// </summary>
    bool IsWaiting { get; set; }

    /// <summary>
    /// Signals that an async operation has completed and event processing should resume.
    /// This method should be called by the event or handler that initiated the WAITING state
    /// to continue with the next phase or handler in the current phase.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and can be called from any thread.
    /// The dispatcher will resume processing on the next available cycle.
    /// </remarks>
    void Resume();
}
