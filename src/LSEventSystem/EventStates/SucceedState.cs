using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Success state for events that completed all business phases successfully.
/// 
/// The SucceedState represents successful completion of all business processing
/// phases without critical failures or cancellations. This state executes
/// success-specific handlers before transitioning to the final CompletedState.
/// 
/// Key Characteristics:
/// - Entered when BusinessState completes without cancellation
/// - Executes success-specific handlers for notifications and finalization
/// - Always reports HasFailures = false and HasCancelled = false
/// - Transitions to CompletedState after handler execution
/// - Handlers are filtered and ordered by priority automatically
/// 
/// Handler Execution:
/// - Automatically loads StateHandlerEntry instances for SucceedState
/// - Filters handlers from event context based on state type
/// - Orders handlers by priority (CRITICAL to BACKGROUND)
/// - Executes conditional handlers after evaluation
/// - Handler failures don't affect success outcome
/// 
/// Usage Scenarios:
/// - Success notifications and confirmations
/// - Final business logic completion tasks
/// - Success metrics and analytics collection
/// - External system integration for successful events
/// - Cache updates and data synchronization
/// - User interface success feedback
/// 
/// State Transitions:
/// - Entry: From BusinessState when all phases succeed
/// - Exit: Always transitions to CompletedState
/// - No loops: Cannot return to processing states
/// 
/// Success Criteria:
/// - All business phases completed without CANCELLED results
/// - No critical failures that require termination
/// - May include recoverable failures that don't prevent success
/// 
/// Thread Safety:
/// - Handler loading and execution is sequential
/// - State properties are immutable after initialization
/// - External control methods return null (no transitions)
/// </summary>
public class SucceedState : IEventProcessState {
    /// <summary>
    /// Reference to the event system context providing access to event data and handlers.
    /// Used for loading success handlers and accessing event processing information.
    /// </summary>
    protected readonly LSEventProcessContext _context;

    /// <summary>
    /// The result of success state processing.
    /// Set to SUCCESS after all success handlers complete execution.
    /// </summary>
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    
    /// <summary>
    /// Indicates if the state has encountered failures during processing.
    /// Always false for SucceedState as it represents successful event completion.
    /// Success handler failures don't affect the overall success outcome.
    /// </summary>
    public bool HasFailures => false;
    
    /// <summary>
    /// Indicates if the state represents a cancelled event.
    /// Always false for SucceedState as successful events are not cancelled.
    /// Events reach this state only when business processing succeeds.
    /// </summary>
    public bool HasCancelled => false;
    
    /// <summary>
    /// Stack of state handlers to execute during success processing.
    /// Populated automatically from context handlers filtered by StateHandlerEntry type.
    /// Ordered by priority with highest priority handlers executed first.
    /// </summary>
    protected Stack<LSStateHandlerEntry> _handlers = new();

    /// <summary>
    /// Constructs a new SucceedState with the provided event system context.
    /// 
    /// Automatically loads and prioritizes all StateHandlerEntry instances
    /// registered for success handling. Handlers are ordered by priority
    /// for consistent execution sequence.
    /// 
    /// Handler Loading Process:
    /// 1. Filter context handlers to only StateHandlerEntry instances
    /// 2. Order handlers by priority (CRITICAL → HIGH → NORMAL → LOW → BACKGROUND)
    /// 3. Push handlers onto stack in reverse order for correct execution
    /// 4. Conditional handlers will be evaluated during execution
    /// </summary>
    /// <param name="context">The event system context containing event and handler information</param>
    public SucceedState(LSEventProcessContext context) {
        _context = context;
        var handlers = _context.Handlers.OfType<LSStateHandlerEntry>().OrderByDescending(h => h.Priority).ToList();
        foreach (var handler in handlers) _handlers.Push(handler);
    }

    /// <summary>
    /// Processes the success by executing all registered success handlers.
    /// 
    /// Success processing provides an opportunity for final business logic,
    /// notifications, and cleanup operations specific to successful events.
    /// After completion, transitions to CompletedState for final processing.
    /// 
    /// Execution Flow:
    /// 1. Execute all StateHandlerEntry instances in priority order
    /// 2. Evaluate conditional handlers before execution
    /// 3. Log handler failures but continue processing
    /// 4. Set StateResult to SUCCESS regardless of handler outcomes
    /// 5. Transition to CompletedState for final cleanup
    /// 
    /// Handler Responsibilities:
    /// - Send success notifications to users or external systems
    /// - Update business metrics and analytics
    /// - Trigger downstream business processes
    /// - Update caches with successful results
    /// - Log success information for audit trails
    /// - Cleanup temporary resources used during processing
    /// 
    /// Error Handling:
    /// - Handler exceptions are caught and logged but don't fail success
    /// - Success outcome is determined by business phase completion
    /// - Individual handler failures don't affect state transitions
    /// - Missing handlers are acceptable (silent success)
    /// 
    /// Performance Considerations:
    /// - Success handlers should be lightweight and fast
    /// - Long-running operations should be deferred to background processes
    /// - Database updates should be efficient and non-blocking when possible
    /// - External service calls should have appropriate timeouts
    /// </summary>
    /// <returns>CompletedState to finalize event processing</returns>
    public IEventProcessState? Process() {
        while (_handlers.Count > 0) {
            var handlerEntry = _handlers.Pop();
            if (!handlerEntry.Condition(_context.Event, handlerEntry)) continue;
            handlerEntry.Handler(_context.Event);
        }

        StateResult = StateProcessResult.SUCCESS;
        return new CompletedState(_context);
    }

    /// <summary>
    /// Resume operation is not supported for SucceedState.
    /// 
    /// Successful events don't have waiting conditions that require resumption.
    /// Success processing is always synchronous and completes immediately.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Resume() {
        return null;
    }

    /// <summary>
    /// Cancel operation is not supported for SucceedState.
    /// 
    /// Events that have succeeded cannot be cancelled as the business
    /// processing has already completed successfully. Success takes precedence.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Cancel() {
        return null;
    }

    /// <summary>
    /// Fail operation is not supported for SucceedState.
    /// 
    /// Successful events cannot be failed as they have already completed
    /// business processing successfully. The success outcome is final.
    /// </summary>
    /// <returns>Always null - no state transitions available</returns>
    public IEventProcessState? Fail() {
        return null;
    }
}
