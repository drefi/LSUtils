namespace LSUtils.ProcessSystem;

/// <summary>
/// Execution priority levels for processing nodes, controlling execution order within hierarchies.
/// <para>
/// LSProcessPriority determines the processing sequence within layer nodes - higher priority
/// values execute first. Within the same priority level, nodes are processed by ascending
/// Order values, providing fine-grained control over execution sequences while maintaining
/// predictable priority-based ordering.
/// </para>
/// <para>
/// <b>Execution Order:</b><br/>
/// Nodes process in descending priority order: CRITICAL (4) → HIGH (3) → NORMAL (2) → LOW (1) → MINIMAL (0)<br/>
/// Within same priority: ascending Order values (0, 1, 2, ...)
/// </para>
/// <para>
/// <b>Priority Guidelines:</b><br/>
/// - CRITICAL: security validation, system integrity, essential prerequisites<br/>
/// - HIGH: core business logic, important state updates, primary workflows<br/>
/// - NORMAL: standard operations, typical business processing (default)<br/>
/// - LOW: optional features, convenience functions, enhancements<br/>
/// - MINIMAL: background tasks, logging, cleanup operations
/// </para>
/// <para>
/// <b>Implementation Notes:</b><br/>
/// All layer nodes (Sequence, Selector, Parallel) sort their children by this priority
/// system before processing, ensuring consistent execution patterns across different
/// node types and processing scenarios.
/// </para>
/// </summary>
public enum LSProcessPriority {
    MINIMAL = 0,

    /// <summary>
    /// Nice-to-have features and optional functionality.
    /// Lower priority - executes after NORMAL, HIGH, and CRITICAL operations.
    /// </summary>
    /// <remarks>
    /// <b>Typical Use Cases:</b><br/>
    /// • User interface enhancements and visual effects<br/>
    /// • Optional feature activation and convenience functions<br/>
    /// • Secondary data processing and derived calculations<br/>
    /// • Non-critical notifications and user feedback<br/>
    /// • Supplementary validation and quality-of-life improvements<br/>
    /// <br/>
    /// <b>Processing Characteristics:</b><br/>
    /// Low priority operations enhance user experience but are not essential for core functionality. They can be deferred during high-load scenarios.
    /// </remarks>
    LOW = 1,

    /// <summary>
    /// Standard operations and normal business logic. Default priority.
    /// Standard priority - executes after HIGH and CRITICAL, before LOW and BACKGROUND.
    /// </summary>
    /// <remarks>
    /// <b>Typical Use Cases:</b><br/>
    /// • Standard business rule processing and workflow execution<br/>
    /// • Regular data manipulation and transformation operations<br/>
    /// • Common user interactions and standard feature implementations<br/>
    /// • Routine calculations and standard algorithm execution<br/>
    /// • Default event handling and typical response processing<br/>
    /// <br/>
    /// <b>Processing Characteristics:</b><br/>
    /// This is the default priority level for most processing nodes. Represents the baseline functionality that should execute in normal operational order without special prioritization.
    /// </remarks>
    NORMAL = 2,

    /// <summary>
    /// Important business logic that should run early in the phase.
    /// High priority - executes after CRITICAL but before NORMAL, LOW, and BACKGROUND.
    /// </summary>
    /// <remarks>
    /// <b>Typical Use Cases:</b><br/>
    /// • Core business logic and primary workflow execution<br/>
    /// • Important state updates and critical data processing<br/>
    /// • Key algorithm execution and performance-sensitive operations<br/>
    /// • Primary user interface updates and essential feedback<br/>
    /// • Important resource allocation and system state management<br/>
    /// <br/>
    /// <b>Processing Characteristics:</b><br/>
    /// High priority operations represent important functionality that should execute early in the processing phase to ensure core system behavior is established before secondary operations.
    /// </remarks>
    HIGH = 3,

    /// <summary>
    /// System-critical operations such as validation and security checks.
    /// Highest priority - executes first before all other priority levels.
    /// </summary>
    /// <remarks>
    /// <b>Typical Use Cases:</b><br/>
    /// • Security validation and authentication checks<br/>
    /// • System integrity verification and safety protocols<br/>
    /// • Essential initialization and prerequisite setup<br/>
    /// • Critical error handling and system protection mechanisms<br/>
    /// • Mandatory compliance checks and regulatory requirements<br/>
    /// <br/>
    /// <b>Processing Characteristics:</b><br/>
    /// Critical operations must execute first to establish system safety, security, and operational prerequisites. Failure at this level typically prevents further processing in the current phase.
    /// </remarks>
    CRITICAL = 4
}
