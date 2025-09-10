using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// State interface for the event processing state machine in v4.
/// Implements the State pattern for clean state management.
/// </summary>
public interface IEventSystemState {

    StateProcessResult StateResult { get; }
    bool HasFailures { get; }
    bool HasCancelled { get; }
    IEventSystemState? Process();
    /// <summary>
    /// Processes the event in this state or resumes processing from a waiting state.
    /// </summary>
    /// <param name="context">The state context.</param>
    IEventSystemState? Resume();

    /// <summary>
    /// Aborts processing from a waiting state.
    /// </summary>
    /// <param name="context">The state context.</param>
    IEventSystemState? Cancel();

    IEventSystemState? Fail();

}
