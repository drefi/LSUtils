using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Immutable handler entry for phase-specific handlers in the LSEventSystem v4.
/// 
/// Represents a configured handler that executes during a specific business phase.
/// Contains all handler metadata including execution logic, priority, conditions,
/// and phase association. Once created, entries are immutable and thread-safe.
/// 
/// Handler entries are created through the PhaseHandlerRegister builder and
/// registered with the dispatcher for automatic execution during event processing.
/// 
/// Key Properties:
/// - Unique identification for tracking and management
/// - Phase type association for execution targeting
/// - Priority-based execution ordering
/// - Conditional execution logic
/// - Handler function with context access
/// 
/// Lifecycle:
/// 1. Created via PhaseHandlerRegister.Build()
/// 2. Registered with dispatcher for phase type
/// 3. Retrieved during event processing
/// 4. Executed when event reaches associated phase
/// 5. Results tracked for phase completion evaluation
/// 
/// Thread Safety:
/// - Immutable after creation
/// - Safe for concurrent access across threads
/// - Handler function should be thread-safe
/// </summary>
public class LSPhaseHandlerEntry : IHandlerEntry {
    /// <summary>
    /// Unique identifier for this handler entry.
    /// Generated automatically at creation time for tracking and management purposes.
    /// </summary>
    public System.Guid ID { get; } = System.Guid.NewGuid();
    
    /// <summary>
    /// The specific phase type this handler executes in.
    /// Used by the phase system to determine which handlers to run during each phase.
    /// Typically one of: ValidatePhaseState, ConfigurePhaseState, ExecutePhaseState, CleanupPhaseState.
    /// </summary>
    public System.Type? PhaseType { get; internal set; }
    
    /// <summary>
    /// Execution priority within the phase.
    /// Determines the order handlers execute: CRITICAL → HIGH → NORMAL → LOW → BACKGROUND.
    /// All handlers in a phase execute regardless of priority, but order may affect results.
    /// </summary>
    public LSPriority Priority { get; internal set; } = LSPriority.NORMAL;
    
    /// <summary>
    /// The main handler function that executes during the phase.
    /// 
    /// Receives EventSystemContext providing access to:
    /// - Event data and metadata
    /// - Dispatcher and other handlers
    /// - Current processing state
    /// 
    /// Must return HandlerProcessResult indicating:
    /// - SUCCESS: Continue processing
    /// - FAILURE: Mark as failed but continue
    /// - WAITING: Pause for external input
    /// - CANCELLED: Critical failure, stop immediately
    /// </summary>
    public Func<LSEventProcessContext, HandlerProcessResult> Handler { get; internal set; } = null!;
    
    /// <summary>
    /// Condition function that determines if this handler should execute.
    /// Evaluated at runtime before handler execution. If false, handler is skipped.
    /// Defaults to always true if not specified during registration.
    /// </summary>
    public Func<ILSEvent, IHandlerEntry, bool> Condition { get; internal set; } = (evt, entry) => true;
    
    /// <summary>
    /// Optional human-readable description of what this handler does.
    /// Used for debugging, logging, and documentation purposes.
    /// Not required for handler execution.
    /// </summary>
    public string? Description { get; internal set; }
    
    /// <summary>
    /// Number of times this handler has been executed.
    /// Incremented each time the handler function is called.
    /// Useful for debugging and monitoring handler usage patterns.
    /// </summary>
    public int ExecutionCount { get; internal set; } = 0;
    
    /// <summary>
    /// Indicates whether this handler can block phase execution when in WAITING state.
    /// When true, a WAITING result from this handler will pause the entire phase.
    /// When false, WAITING results are treated as SUCCESS for flow control.
    /// Default is false for non-blocking behavior.
    /// </summary>
    public bool WaitingBlockExecution { get; internal set; } = false;
}
