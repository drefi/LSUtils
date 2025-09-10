using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Immutable handler entry for state-specific handlers in the LSEventSystem v4.
/// 
/// Represents a configured handler that executes when events enter specific states
/// in the state machine. State handlers typically perform finalization, cleanup,
/// logging, or notification operations after business logic completion.
/// 
/// Unlike phase handlers, state handlers don't control flow or return results -
/// they are "fire and forget" operations that run when state transitions occur.
/// 
/// Key Properties:
/// - Unique identification for tracking and management
/// - State type association for execution targeting
/// - Priority-based execution ordering within states
/// - Conditional execution logic
/// - Simple action-based handler (no return value)
/// 
/// Lifecycle:
/// 1. Created via StateHandlerRegister.Build()
/// 2. Registered with dispatcher for state type
/// 3. Retrieved during state transitions
/// 4. Executed when event enters associated state
/// 5. No result tracking (fire-and-forget pattern)
/// 
/// Thread Safety:
/// - Immutable after creation
/// - Safe for concurrent access across threads
/// - Handler action should be thread-safe
/// </summary>
public class LSStateHandlerEntry : IHandlerEntry {
    /// <summary>
    /// Unique identifier for this handler entry.
    /// Generated automatically at creation time for tracking and management purposes.
    /// </summary>
    public System.Guid ID { get; } = System.Guid.NewGuid();
    
    /// <summary>
    /// The specific state type this handler executes in.
    /// Used by the state system to determine which handlers to run during state transitions.
    /// Typically one of: SucceedState, CancelledState, CompletedState.
    /// </summary>
    public System.Type? StateType { get; internal set; }
    
    /// <summary>
    /// Execution priority within the state.
    /// Determines the order handlers execute: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND.
    /// Important for finalization order when multiple handlers modify shared resources.
    /// </summary>
    public LSPriority Priority { get; internal set; } = LSPriority.NORMAL;
    
    /// <summary>
    /// The main handler action that executes when the event enters the state.
    /// 
    /// Receives the event for state-specific processing such as:
    /// - Success notifications and logging
    /// - Cleanup and resource disposal
    /// - Metrics and audit trail updates
    /// - External system notifications
    /// 
    /// State handlers are "fire and forget" - they don't return results
    /// or affect state transitions. Exceptions are logged but don't stop processing.
    /// </summary>
    public LSAction<ILSEvent> Handler { get; internal set; } = null!;
    
    /// <summary>
    /// Condition function that determines if this handler should execute.
    /// Evaluated at runtime before handler execution. If false, handler is skipped.
    /// Defaults to always true if not specified during registration.
    /// </summary>
    public Func<ILSEvent, IHandlerEntry, bool> Condition { get; internal set; } = (evt, entry) => true;
    
    /// <summary>
    /// Number of times this handler has been executed.
    /// Incremented each time the handler action is called.
    /// Useful for debugging and monitoring handler usage patterns.
    /// </summary>
    public int ExecutionCount { get; internal set; } = 0;
}
