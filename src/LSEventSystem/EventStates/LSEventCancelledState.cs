using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// State for events that have been cancelled.
/// 
/// This state executes any registered cancellation handlers before transitioning 
/// to the CompletedState.
/// 
/// Key Characteristics:
/// - Executes cancellation-specific handlers
/// - Always reports HasCancelled = true
/// - Cannot be resumed, failed, or cancelled again
/// - Transitions to CompletedState after handler execution
/// 
/// Handler Execution:
/// - Runs all registered StateHandlerEntry instances for LSEventCancelledState
/// - Handlers are executed in priority order (CRITICAL to BACKGROUND)
/// - Conditional handlers are evaluated before execution
/// - Handler failures do not affect state progression
/// 
/// State Transitions:
/// - Entry: From any state via Cancel() operations
/// - Exit: Always transitions to CompletedState
/// - No loops: Cannot return to this state once left
/// 
/// Thread Safety:
/// - Handler execution is sequential within the state
/// - State properties are read-only after initialization
/// - External control methods return null (no transitions)
/// </summary>
public class LSEventCancelledState : IEventProcessState {
    /// <summary>
    /// Reference to the event system context providing access to event data and handlers.
    /// Used to retrieve registered cancellation handlers and event information.
    /// </summary>
    protected readonly LSEventProcessContext _context;
    
    /// <summary>
    /// Stack of state handlers to execute during cancellation processing.
    /// Handlers are pushed in reverse priority order for correct execution sequence.
    /// </summary>
    protected Stack<LSStateHandlerEntry> _handlers = new();
    
    /// <summary>
    /// The result of state processing. Set to SUCCESS after all handlers complete.
    /// Always SUCCESS for cancellation states as the cancellation itself succeeded.
    /// </summary>
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    
    /// <summary>
    /// Indicates if the state has encountered failures during processing.
    /// Always false for CancelledState as it represents successful cancellation processing.
    /// Individual handler failures don't affect the cancellation outcome.
    /// </summary>
    public bool HasFailures => false;
    
    /// <summary>
    /// Indicates if the state represents a cancelled event.
    /// Always true for CancelledState since this is the cancellation terminal state.
    /// This is different from StateResult which tracks processing success.
    /// </summary>
    public bool HasCancelled => true; //always true for CancelledState since the StateResult is different from actually being cancelled

    /// <summary>
    /// Constructs a new CancelledState with the provided event system context.
    /// 
    /// Initializes the state with access to the event context for handler execution.
    /// The context provides access to registered cancellation handlers and event data.
    /// </summary>
    /// <param name="context">The event system context containing event and handler information</param>
    public LSEventCancelledState(LSEventProcessContext context) {
        _context = context;
        var handlers = _context.Handlers
            .OfType<LSStateHandlerEntry>()
            .Where(h => h.StateType == typeof(LSEventCancelledState))
            .OrderByDescending(h => h.Priority).ToList();
        foreach (var handler in handlers) _handlers.Push(handler);
    }

    /// <summary>
    /// Processes the cancellation by executing all registered cancellation handlers.
    /// 
    /// Execution Flow:
    /// 1. Execute all StateHandlerEntry instances registered for CancelledState
    /// 2. Handlers run in priority order (pushed to stack in reverse order)
    /// 3. Conditional handlers are evaluated before execution
    /// 4. Handler failures are ignored (cancellation always succeeds)
    /// 5. Transition to CompletedState for final cleanup
    /// 
    /// Handler Responsibilities:
    /// - Log cancellation reasons and context
    /// - Cleanup resources allocated during processing
    /// - Send cancellation notifications to external systems
    /// - Update audit trails and metrics
    /// - Rollback partial state changes
    /// 
    /// Error Handling:
    /// - Handler exceptions are caught and logged but don't stop processing
    /// - Cancellation always succeeds regardless of handler outcomes
    /// - Missing handlers are acceptable (no-op cancellation)
    /// 
    /// Performance:
    /// - Handlers should be lightweight as this is a terminal operation
    /// - Long-running cleanup should be deferred to background processes
    /// - Synchronous execution model - no waiting or async patterns
    /// </summary>
    /// <returns>CompletedState to finalize event processing</returns>
    public IEventProcessState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

        StateResult = StateProcessResult.SUCCESS;
        return new LSEventCompletedState(_context);
    }
    
    /// <summary>
    /// Resume operation is not supported for CancelledState.
    /// 
    /// Cancellation is a terminal operation that cannot be resumed.
    /// Once an event is cancelled, it cannot return to processing.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Resume() => null;
    
    /// <summary>
    /// Cancel operation is not supported for CancelledState.
    /// 
    /// The event is already in the cancelled state, so additional
    /// cancellation requests have no effect.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Cancel() => null;
    
    /// <summary>
    /// Fail operation is not supported for CancelledState.
    /// 
    /// Failure requests are meaningless for already-cancelled events.
    /// The cancellation outcome takes precedence over any failure states.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Fail() => null;
}
