namespace LSUtils;

/// <summary>
/// Defines priority levels within each phase for fine-grained ordering control.
/// </summary>
/// <remarks>
/// Priority determines the execution order of listeners within the same phase.
/// Lower priority values execute first, higher values execute later.
/// </remarks>
public enum PhasePriority {
    /// <summary>
    /// Highest priority - executes first within phase.
    /// Use for critical operations that must happen before anything else.
    /// </summary>
    CRITICAL = -100,
    
    /// <summary>
    /// High priority - executes early within phase.
    /// Use for important operations that should happen before most others.
    /// </summary>
    HIGH = -50,
    
    /// <summary>
    /// Normal priority - default execution order.
    /// Use for standard operations with no special ordering requirements.
    /// </summary>
    NORMAL = 0,
    
    /// <summary>
    /// Low priority - executes late within phase.
    /// Use for operations that should happen after most others.
    /// </summary>
    LOW = 50,
    
    /// <summary>
    /// Lowest priority - executes last within phase.
    /// Use for cleanup, logging, or operations that must happen after everything else.
    /// </summary>
    MINIMAL = 100
}
