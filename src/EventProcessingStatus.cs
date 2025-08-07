namespace LSUtils;

/// <summary>
/// Defines the possible outcomes of event listener processing.
/// </summary>
/// <remarks>
/// This enum provides a clean, declarative way for listeners to indicate
/// the result of their processing without needing to explicitly call
/// signal/failure/cancel methods on the event. Inspired by behavior tree status patterns.
/// </remarks>
public enum EventProcessingStatus {
    /// <summary>
    /// The listener processed the event successfully.
    /// Equivalent to calling event.Signal().
    /// The event will continue to the next phase or listener.
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// The listener encountered a failure during processing.
    /// Equivalent to calling event.Failure(message).
    /// The event will skip to the FAILURE phase.
    /// </summary>
    FAILURE,
    
    /// <summary>
    /// The listener requests that the event be cancelled.
    /// Equivalent to calling event.Cancel().
    /// The event will skip to the CANCEL phase.
    /// </summary>
    CANCEL,
    
    /// <summary>
    /// The listener is still processing and needs more time.
    /// Does not signal completion - used for async operations.
    /// The listener is responsible for calling event.Signal(), event.Failure(), 
    /// or event.Cancel() when the async operation completes.
    /// </summary>
    RUNNING,
    
    /// <summary>
    /// The listener chooses not to process this event.
    /// Does not affect the event's completion state.
    /// The event continues processing with other listeners.
    /// </summary>
    SKIP
}
