namespace LSUtils.ProcessSystem;

/// <summary>
/// Defines execution priority levels for nodes within the LSProcessing system.
/// Priority determines processing order within the same execution phase - higher priority values execute first.
/// </summary>
/// <remarks>
/// <b>Priority Execution Order:</b><br/>
/// Nodes are processed in descending priority order (CRITICAL → BACKGROUND), then by ascending Order value within the same priority.<br/>
/// <br/>
/// <b>Use Cases by Priority Level:</b><br/>
/// • <b>CRITICAL:</b> Security validation, system integrity checks, essential initialization<br/>
/// • <b>HIGH:</b> Core business logic, primary workflows, important state updates<br/>
/// • <b>NORMAL:</b> Standard processing, typical business operations, default behavior<br/>
/// • <b>LOW:</b> Optional enhancements, convenience features, non-essential processing<br/>
/// • <b>BACKGROUND:</b> Logging, metrics collection, cleanup tasks, maintenance operations<br/>
/// <br/>
/// <b>Processing Pattern:</b><br/>
/// Within layer nodes (Sequence, Selector, Parallel), children are sorted by Priority (descending) then Order (ascending) to ensure predictable execution sequences while allowing fine-grained control within priority levels.
/// </remarks>
public enum LSProcessPriority {
    /// <summary>
    /// Background operations like logging, metrics, and non-critical cleanup.
    /// Lowest priority - executes last in processing order.
    /// </summary>
    /// <remarks>
    /// <b>Typical Use Cases:</b><br/>
    /// • Performance metrics collection and telemetry reporting<br/>
    /// • Debug logging and trace information recording<br/>
    /// • Memory cleanup and garbage collection hints<br/>
    /// • Cache maintenance and optimization tasks<br/>
    /// • Non-essential file I/O operations<br/>
    /// <br/>
    /// <b>Processing Characteristics:</b><br/>
    /// Background tasks should never affect core application functionality and can be safely delayed or skipped if system resources are constrained.
    /// </remarks>
    BACKGROUND = 0,

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
