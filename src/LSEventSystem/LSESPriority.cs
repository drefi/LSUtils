namespace LSUtils.EventSystem;

/// <summary>
/// Execution priority within a phase. Lower numeric values execute first.
/// </summary>
public enum LSESPriority {
    /// <summary>
    /// Background operations like logging, metrics, and non-critical cleanup.
    /// </summary>
    BACKGROUND = 0,

    /// <summary>
    /// Nice-to-have features and optional functionality.
    /// </summary>
    LOW = 1,

    /// <summary>
    /// Standard operations and normal business logic. Default priority.
    /// </summary>
    NORMAL = 2,

    /// <summary>
    /// Important business logic that should run early in the phase.
    /// </summary>
    HIGH = 3,

    /// <summary>
    /// System-critical operations such as validation and security checks.
    /// </summary>
    CRITICAL = 4
}
