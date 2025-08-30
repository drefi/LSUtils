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
internal interface ILSMutableEvent : ILSEvent {
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
    /// Gets or sets the current phase being executed.
    /// Updated by the dispatcher as it progresses through the event processing pipeline.
    /// </summary>
    new LSEventPhase CurrentPhase { get; set; }

    /// <summary>
    /// Gets or sets the flags indicating which phases have been completed successfully.
    /// Uses bitwise operations to efficiently track completion state across multiple phases.
    /// Updated by the dispatcher when each phase completes without errors or cancellation.
    /// </summary>
    new LSEventPhase CompletedPhases { get; set; }

    /// <summary>
    /// Gets or sets whether the event is currently waiting for an async operation to complete.
    /// When true, the dispatcher pauses processing until Resume(), Abort(), or Fail() is called.
    /// Automatically set when a handler returns LSPhaseResult.WAITING.
    /// </summary>
    bool IsWaiting { get; set; }

    /// <summary>
    /// Signals that an async operation has completed and event processing should resume.
    /// Thread-safe method that can be called from any thread.
    /// The dispatcher will resume processing on the next available cycle.
    /// </summary>
    new void Resume();

    /// <summary>
    /// Signals that an async operation has failed and event processing should be cancelled.
    /// Thread-safe method that can be called from any thread.
    /// The dispatcher will cancel the event and proceed to the CANCEL phase.
    /// </summary>
    new void Abort();

    /// <summary>
    /// Signals that an async operation has failed but event processing should continue.
    /// Thread-safe method that can be called from any thread.
    /// The dispatcher will mark the event as having failures and proceed to the FAILURE phase.
    /// </summary>
    new void Fail();
}
