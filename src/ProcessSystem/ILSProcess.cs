namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
/// <summary>
/// Defines the contract for executable processes within the LSProcessSystem.
/// <para>
/// A process represents a unit of work that carries data through a configurable processing pipeline.
/// Processes are state containers that can be executed against registered node hierarchies,
/// supporting complex workflows with branching, parallel execution, and async operations.
/// </para>
/// <para>
/// <b>Key Characteristics:</b><br/>
/// - Immutable metadata (ID, CreatedAt) with mutable execution state<br/>
/// - Internal key-value data store for inter-handler communication<br/>
/// - Optional custom processing tree that merges with registered global contexts<br/>
/// - Single execution model with support for Resume/Fail operations on waiting nodes<br/>
/// - Built-in cancellation and completion tracking
/// </para>
/// <para>
/// <b>Execution Flow:</b><br/>
/// 1. Process created with auto-generated ID and timestamp<br/>
/// 2. Optional WithProcessing() call to define custom processing logic<br/>
/// 3. Execute() creates session and runs through merged node hierarchy<br/>
/// 4. Process may enter WAITING state for external events<br/>
/// 5. Resume()/Fail() methods handle asynchronous continuation<br/>
/// 6. Process reaches terminal state (SUCCESS, FAILURE, CANCELLED)
/// </para>
/// </summary>
/// <example>
/// Basic process usage with custom processing logic:
/// <code>
/// public class MyCustomProcess : LSProcess {
///     public string ProcessData { get; set; }
/// }
/// 
/// var process = new MyCustomProcess { ProcessData = "test" };
/// 
/// // Optional: Add custom processing logic
/// process.WithProcessing(builder => builder
///     .Sequence("main", seq => seq
///         .Handler("validate-data", session => {
///             var data = session.Process.GetData&lt;string&gt;("processData");
///             return string.IsNullOrEmpty(data) ? 
///                 LSProcessResultStatus.FAILURE : 
///                 LSProcessResultStatus.SUCCESS;
///         })
///         .Handler("process-data", session => {
///             // Process the data
///             return LSProcessResultStatus.SUCCESS;
///         })
///     )
/// );
/// 
/// // Execute the process
/// var result = process.Execute();
/// </code>
/// </example>
public interface ILSProcess {
    /// <summary>
    /// Unique identifier automatically assigned at process creation.
    /// Used for tracking, debugging, and associating processes with execution sessions.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// UTC timestamp when this process instance was created.
    /// Used for execution timing analysis, debugging, and audit trails.
    /// </summary>
    System.DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates if the process has been cancelled and cannot continue execution.
    /// 
    /// Implementation Note: Determined by checking the root node status of the current
    /// execution session. Returns false if no session exists (process not yet executed).
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates if the process has reached a terminal execution state.
    /// 
    /// Returns true when the root node status is SUCCESS, FAILURE, or CANCELLED.
    /// Returns false if no execution session exists or if still in UNKNOWN/WAITING state.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Stores typed data in the process's internal dictionary for inter-handler communication.
    /// 
    /// Data persists for the lifetime of the process and can be accessed by any handler
    /// in the processing pipeline. Overwrites existing values with the same key.
    /// </summary>
    /// <param name="key">String key for the data (case-sensitive)</param>
    /// <param name="value">Typed value to store</param>
    void SetData<T>(string key, T value);

    /// <summary>
    /// Retrieves strongly-typed data from the process's internal dictionary.
    /// 
    /// Throws exceptions for missing keys or type mismatches to ensure data integrity
    /// in controlled environments where data presence is guaranteed.
    /// </summary>
    /// <typeparam name="T">Expected type of the stored data</typeparam>
    /// <param name="key">String key for the data</param>
    /// <returns>The stored value cast to type T</returns>
    /// <exception cref="KeyNotFoundException">Key not found in internal dictionary</exception>
    /// <exception cref="LSException">Stored value cannot be cast to type T</exception>
    T? GetData<T>(string key);

    /// <summary>
    /// Safely attempts to retrieve typed data without throwing exceptions.
    /// 
    /// Preferred method for optional data access or when data presence is uncertain.
    /// Returns false for both missing keys and type conversion failures.
    /// </summary>
    /// <typeparam name="T">Expected type of the stored data</typeparam>
    /// <param name="key">String key for the data</param>
    /// <param name="value">Output parameter containing the value or default(T)</param>
    /// <returns>True if key exists and value can be cast to T, otherwise false</returns>
    bool TryGetData<T>(string key, out T? value);

    /// <summary>
    /// Defines custom processing logic specific to this process instance.
    /// <para>
    /// The provided builder creates a local processing tree that will be merged with
    /// global and instance-specific contexts during execution. This enables process-specific
    /// workflow customization while leveraging shared processing infrastructure.
    /// </para>
    /// <para>
    /// <b>Context Merge Priority (highest to lowest):</b><br/>
    /// 1. Local context (this method)<br/>
    /// 2. Instance-specific context (registered with LSProcessManager)<br/>
    /// 3. Global context (registered with LSProcessManager)
    /// </para>
    /// <para>
    /// Can be called multiple times - subsequent calls extend the existing tree.
    /// </para>
    /// </summary>
    /// <param name="builder">Delegate that builds the processing node hierarchy</param>
    /// <param name="layerType">Type of root layer node (PARALLEL, SEQUENCE, SELECTOR)</param>
    /// <returns>This process instance for method chaining</returns>
    /// <example>
    /// Adding process-specific validation and processing logic:
    /// <code>
    /// var process = new MyCustomProcess();
    /// 
    /// process.WithProcessing(builder => builder
    ///     .Sequence("validation-and-processing", seq => seq
    ///         .Handler("validate-input", session => {
    ///             var input = session.Process.TryGetData&lt;string&gt;("input", out var value) ? value : "";
    ///             return string.IsNullOrEmpty(input) ? 
    ///                 LSProcessResultStatus.FAILURE : 
    ///                 LSProcessResultStatus.SUCCESS;
    ///         })
    ///         .Handler("transform-data", session => {
    ///             // Process and transform the data
    ///             session.Process.SetData("result", "processed_data");
    ///             return LSProcessResultStatus.SUCCESS;
    ///         })
    ///     )
    /// );
    /// 
    /// var result = process.Execute();
    /// </code>
    /// </example>
    ILSProcess WithProcessing(LSProcessBuilderAction builder, LSProcessLayerNodeType layerType = LSProcessLayerNodeType.PARALLEL);

    /// <summary>
    /// Executes the process through the registered processing pipeline.
    /// 
    /// Creates an execution session and processes through the merged node hierarchy
    /// (local + instance-specific + global contexts). This is a single-use operation -
    /// subsequent calls return the cached result from the first execution.
    /// 
    /// Execution Steps:
    /// 1. Get merged root node from LSProcessManager
    /// 2. Create LSProcessSession with this process as data carrier
    /// 3. Execute session through node hierarchy
    /// 4. Return final status (may be WAITING for async operations)
    /// 
    /// The process serves as the data container while the session manages execution state.
    /// </summary>
    /// <param name="instance">Target processable instance (affects context resolution)</param>
    /// <param name="manager">Process manager for context resolution (uses singleton if null)</param>
    /// <returns>Final execution status or WAITING if async operations are pending</returns>
    /// <exception cref="LSException">Thrown if called multiple times on the same process</exception>
    LSProcessResultStatus Execute(ILSProcessable? instance = null, LSProcessManager? manager = null);

    /// <summary>
    /// Resumes execution of nodes currently in WAITING state.
    /// 
    /// Used for asynchronous operations where handlers return WAITING and external
    /// events later trigger continuation. Delegates to the execution session's
    /// Resume method to locate and transition waiting nodes.
    /// 
    /// Behavior:
    /// - Empty nodeIDs array: Resume all nodes currently in WAITING state
    /// - Specific nodeIDs: Resume only the specified nodes if they are waiting
    /// - Invalid/non-waiting nodeIDs are ignored
    /// </summary>
    /// <param name="nodeIDs">Specific node identifiers to resume (empty = all waiting)</param>
    /// <returns>Updated process status after resumption attempt</returns>
    /// <exception cref="LSException">Thrown if process has not been executed yet</exception>
    LSProcessResultStatus Resume(params string[] nodeIDs);

    /// <summary>
    /// Forces nodes in WAITING state to transition to FAILURE.
    /// 
    /// Used for timeout handling, error conditions, or explicit cancellation of
    /// waiting operations. Delegates to the execution session's Fail method.
    /// 
    /// Behavior:
    /// - Empty nodeIDs array: Fail all nodes currently in WAITING state
    /// - Specific nodeIDs: Fail only the specified nodes if they are waiting
    /// - May trigger cascading failures in parent nodes based on their logic
    /// </summary>
    /// <param name="nodeIDs">Specific node identifiers to fail (empty = all waiting)</param>
    /// <returns>Updated process status after failure injection</returns>
    /// <exception cref="LSException">Thrown if process has not been executed yet</exception>
    LSProcessResultStatus Fail(params string[] nodeIDs);

    /// <summary>
    /// Immediately cancels the entire process execution tree.
    /// 
    /// Sets the root node to CANCELLED state, which cascades through all child nodes.
    /// This is a terminal operation - cancelled processes cannot be resumed.
    /// 
    /// Implementation delegates to the execution session's Cancel method.
    /// </summary>
    /// <exception cref="LSException">Thrown if process has not been executed yet</exception>
    void Cancel();
}
