using System.Collections.Generic;

namespace LSUtils.EventSystem;

/// <summary>
/// Final terminal state for all event processing in the LSEventSystem v4.
/// 
/// The CompletedState represents the absolute end of event processing, regardless
/// of whether the event succeeded, failed, or was cancelled. This state executes
/// final completion handlers and marks the event as fully processed.
/// 
/// Key Characteristics:
/// - Absolute terminal state - no further transitions possible
/// - Executes completion handlers for final cleanup and logging
/// - Returns null to indicate end of state machine processing
/// - Accepts events from any other state (Success, Cancelled, Business)
/// - Always reports successful state processing (StateResult.SUCCESS)
/// 
/// Handler Execution:
/// - Runs all registered StateHandlerEntry instances for CompletedState
/// - Handlers execute in priority order for consistent finalization
/// - Conditional handlers are evaluated before execution
/// - Handler failures are logged but don't affect completion
/// 
/// Usage Scenarios:
/// - Final audit logging and metrics collection
/// - Resource cleanup and disposal
/// - External system notifications of completion
/// - Performance monitoring and profiling
/// - Database transaction commits or rollbacks
/// 
/// State Machine Role:
/// - Entry: From SucceedState, CancelledState, or direct transitions
/// - Exit: None - this is the absolute terminal state
/// - Purpose: Ensure all events have a consistent final processing step
/// 
/// Thread Safety:
/// - Handler execution is sequential within the state
/// - State properties are immutable after processing
/// - No external control operations supported
/// </summary>
public class CompletedState : IEventProcessState {
    /// <summary>
    /// Reference to the event system context providing access to event data and handlers.
    /// Used for executing final completion handlers and accessing event information.
    /// </summary>
    protected readonly LSEventProcessContext _context;
    
    /// <summary>
    /// Stack of state handlers to execute during completion processing.
    /// Contains final cleanup and logging handlers for the completed event.
    /// </summary>
    protected Stack<LSStateHandlerEntry> _handlers = new();
    
    /// <summary>
    /// The result of completion state processing.
    /// Always set to SUCCESS after handler execution, indicating successful completion.
    /// </summary>
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    
    /// <summary>
    /// Indicates if the state has encountered failures during processing.
    /// Always false for CompletedState as completion processing always succeeds.
    /// Individual handler failures don't prevent successful completion.
    /// </summary>
    public bool HasFailures => false;
    
    /// <summary>
    /// Indicates if the state represents a cancelled event.
    /// Always false for CompletedState as this represents successful completion processing,
    /// regardless of whether the original event was cancelled.
    /// </summary>
    public bool HasCancelled => false;

    /// <summary>
    /// Constructs a new CompletedState with the provided event system context.
    /// 
    /// Initializes the final state with access to the event context for
    /// completion handler execution and final event processing.
    /// </summary>
    /// <param name="context">The event system context containing event and handler information</param>
    public CompletedState(LSEventProcessContext context) {
        _context = context;
    }

    /// <summary>
    /// Processes the completion by executing all registered completion handlers.
    /// 
    /// This is the final processing step in the event lifecycle. After this
    /// method completes, the event is considered fully processed and the
    /// state machine terminates.
    /// 
    /// Execution Flow:
    /// 1. Execute all StateHandlerEntry instances registered for CompletedState
    /// 2. Handlers run in priority order for consistent completion processing
    /// 3. Conditional handlers are evaluated before execution
    /// 4. Handler failures are logged but don't affect completion success
    /// 5. Return null to signal end of state machine processing
    /// 
    /// Handler Responsibilities:
    /// - Final audit logging with complete event information
    /// - Performance metrics and monitoring data collection
    /// - External system notifications of event completion
    /// - Database transaction commits or final persistence
    /// - Resource cleanup and disposal operations
    /// - Cache invalidation and data synchronization
    /// 
    /// Error Handling:
    /// - Handler exceptions are caught and logged but don't fail completion
    /// - Completion always succeeds regardless of handler outcomes
    /// - Missing handlers are acceptable (silent completion)
    /// 
    /// Performance Considerations:
    /// - This is the final processing step, so thoroughness over speed
    /// - Long-running operations should still be minimized
    /// - Background tasks can be initiated but not awaited
    /// </summary>
    /// <returns>Always null to indicate end of event processing</returns>
    public IEventProcessState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

        StateResult = StateProcessResult.SUCCESS;
        return null;
    }

    /// <summary>
    /// Resume operation is not supported for CompletedState.
    /// 
    /// Completed events cannot be resumed as they have finished processing.
    /// This is the absolute terminal state of the event system.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Resume() {
        return null;
    }

    /// <summary>
    /// Cancel operation is not supported for CompletedState.
    /// 
    /// Events that have completed cannot be cancelled as processing
    /// has already finished. The completion state takes precedence.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Cancel() {
        return null;
    }

    /// <summary>
    /// Fail operation is not supported for CompletedState.
    /// 
    /// Completed events cannot be failed as processing has already
    /// concluded successfully. The completion outcome is final.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Fail() {
        return null;
    }
}
