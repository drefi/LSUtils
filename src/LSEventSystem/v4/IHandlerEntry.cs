using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Base interface for all handler entries in the LSEventSystem v4.
/// 
/// Defines the common contract that all handler types must implement,
/// providing essential metadata for handler identification, execution
/// ordering, and conditional processing.
/// 
/// This interface is implemented by:
/// - <see cref="PhaseHandlerEntry"/>: Handlers that execute during business phases
/// - <see cref="StateHandlerEntry"/>: Handlers that execute during state transitions
/// 
/// The interface enables polymorphic handling of different handler types
/// while maintaining type safety through the implementation classes.
/// 
/// Core Responsibilities:
/// - Unique identification for tracking and management
/// - Priority-based execution ordering
/// - Conditional execution logic
/// - Execution tracking and monitoring
/// 
/// Usage:
/// This interface is primarily used internally by the event system
/// infrastructure. Client code typically interacts with the concrete
/// handler entry types through the registration builders.
/// </summary>
public interface IHandlerEntry {
    /// <summary>
    /// Unique identifier for this handler entry.
    /// 
    /// Used for:
    /// - Handler tracking and management
    /// - Registration/unregistration operations
    /// - Debugging and monitoring
    /// - Avoiding duplicate registrations
    /// 
    /// Generated automatically when handler entries are created
    /// and remains constant throughout the handler's lifetime.
    /// </summary>
    System.Guid ID { get; }
    
    /// <summary>
    /// Execution priority within the handler's execution context.
    /// 
    /// Determines the order handlers execute within phases or states:
    /// - CRITICAL (0): System-critical operations, security checks
    /// - HIGH (1): Important business logic
    /// - NORMAL (2): Standard operations (default)
    /// - LOW (3): Nice-to-have features
    /// - BACKGROUND (4): Logging, metrics, cleanup
    /// 
    /// Handlers with the same priority may execute in any order.
    /// Priority only affects order, not whether handlers execute.
    /// </summary>
    LSESPriority Priority { get; }
    
    /// <summary>
    /// Condition function that determines if this handler should execute.
    /// 
    /// Evaluated at runtime before handler execution. Takes the current
    /// event and this handler entry as parameters, returns true if the
    /// handler should execute, false to skip.
    /// 
    /// Common Usage:
    /// - Feature flags: evt.GetData&lt;bool&gt;("featureEnabled")
    /// - Environment checks: evt.GetData&lt;string&gt;("env") == "production"
    /// - Data validation: evt.HasData("requiredField")
    /// - Custom business logic: evt.GetData&lt;decimal&gt;("amount") > 100
    /// 
    /// Performance: Should be lightweight as evaluated for every handler.
    /// Default: Returns true (always execute) if not specified.
    /// </summary>
    Func<ILSEvent, IHandlerEntry, bool> Condition { get; }
    
    /// <summary>
    /// Number of times this handler has been executed.
    /// 
    /// Automatically incremented each time the handler function or action
    /// is called. Useful for:
    /// - Debugging handler execution patterns
    /// - Monitoring system performance
    /// - Detecting unused or over-used handlers
    /// - Performance analysis and optimization
    /// 
    /// Reset behavior: Not automatically reset, maintains count for
    /// the lifetime of the handler entry.
    /// </summary>
    int ExecutionCount { get; }
}
