namespace LSUtils.Processing;

using System;
using System.Collections.Generic;

/// <summary>
/// Abstract base class for all processes in the LSProcessing system.
/// Provides core functionality for process execution, state management, and data storage.
/// </summary>
/// <remarks>
/// <para><strong>Process Lifecycle:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Creation</strong>: Process is instantiated with unique ID and timestamp</description></item>
/// <item><description><strong>Configuration</strong>: Optional processing tree setup via WithProcessing()</description></item>
/// <item><description><strong>Execution</strong>: Process is executed through Execute() method</description></item>
/// <item><description><strong>State Management</strong>: Process can be resumed, failed, or cancelled during execution</description></item>
/// </list>
/// 
/// <para><strong>Data Management:</strong></para>
/// <para>Processes include a built-in key-value data store for carrying information throughout</para>
/// <para>the processing pipeline. Data is strongly typed and provides both exception-based</para>
/// <para>and try-pattern access methods.</para>
/// 
/// <para><strong>Processing Tree Integration:</strong></para>
/// <para>Processes can have custom processing trees defined via WithProcessing(), which are</para>
/// <para>merged with global context during execution for flexible workflow customization.</para>
/// 
/// <para><strong>Execution Sessions:</strong></para>
/// <para>Each process maintains an internal execution session (_processSession) that manages</para>
/// <para>the actual processing state and coordinates with the node hierarchy.</para>
/// </remarks>
public abstract class LSProcess : ILSProcess {
    /// <summary>
    /// The current execution session for this process. Null until Execute() is called.
    /// </summary>
    private LSProcessSession? _processSession;

    /// <summary>
    /// Internal data store for process-specific key-value pairs.
    /// </summary>
    private Dictionary<string, object> _data = new();

    /// <summary>
    /// Custom processing context/tree defined for this specific process instance.
    /// Merged with global context during execution.
    /// </summary>
    private ILSProcessLayerNode? _localProcessBuilder;

    /// <summary>
    /// Unique identifier for this process instance.
    /// Generated automatically when the process is created.
    /// </summary>
    public Guid ID { get; }

    /// <summary>
    /// UTC timestamp when this process was created.
    /// Used for timing analysis and audit trails.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates whether this process has been cancelled.
    /// Set by the Cancel() method and prevents further processing.
    /// </summary>
    public bool IsCancelled { get; protected set; }

    /// <summary>
    /// Indicates whether this process has encountered failures during execution.
    /// Set during processing based on node execution results.
    /// </summary>
    public bool HasFailures { get; protected set; }

    /// <summary>
    /// Indicates whether this process has completed execution successfully.
    /// Set when the processing pipeline reaches a terminal success state.
    /// </summary>
    public bool IsCompleted { get; protected set; }

    /// <summary>
    /// Initializes a new process instance with auto-generated ID and creation timestamp.
    /// </summary>
    /// <remarks>
    /// This constructor is protected to enforce the abstract nature of the class.
    /// Derived classes should implement specific process types.
    /// </remarks>
    protected LSProcess() {
        ID = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Retrieves strongly-typed data associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored data cannot be cast to the specified type.</exception>
    public virtual T GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value)) {
            if (value is T tValue) {
                return tValue;
            }
            throw new InvalidCastException($"Stored data with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"No data found for key '{key}'.");
    }

    /// <summary>
    /// Executes this process through the processing pipeline.
    /// Creates a new execution session and processes through the node hierarchy.
    /// </summary>
    /// <param name="instance">Optional processable instance to use as context target.</param>
    /// <param name="manager">Optional process manager to use. Uses singleton if not provided.</param>
    /// <returns>The final processing status (SUCCESS, FAILURE, WAITING, CANCELLED).</returns>
    /// <exception cref="LSException">Thrown if the process has already been executed.</exception>
    /// <remarks>
    /// <para>This method can only be called once per process instance. Subsequent calls will throw an exception.</para>
    /// <para>The method merges any custom processing context with the global context before execution.</para>
    /// </remarks>
    public LSProcessResultStatus Execute(ILSProcessable? instance = null, LSProcessManager? manager = null) {
        manager ??= LSProcessManager.Singleton;
        if (_processSession != null) throw new LSException("Process already executed.");

        var globalBuilder = manager.GetRootNode(this.GetType(), instance, _localProcessBuilder);
        _processSession = new LSProcessSession(this, globalBuilder);
        return _processSession.Execute();
    }

    /// <summary>
    /// Resumes processing from WAITING state for the specified node IDs.
    /// </summary>
    /// <param name="nodeIDs">Array of specific node IDs to resume. If empty, resumes all waiting nodes.</param>
    /// <returns>The processing status after resume attempt.</returns>
    /// <exception cref="LSException">Thrown if the process has not been executed yet.</exception>
    public LSProcessResultStatus Resume(params string[] nodeIDs) {
        if (_processSession == null) {
            throw new LSException("Process not yet executed.");
        }
        return _processSession.Resume(nodeIDs);
    }

    /// <summary>
    /// Cancels the entire process execution immediately.
    /// Sets the process to CANCELLED state and prevents any further processing.
    /// </summary>
    /// <exception cref="LSException">Thrown if the process has not been executed yet.</exception>
    /// <remarks>
    /// This is a terminal operation that cannot be undone. Once cancelled, 
    /// the process cannot be resumed or continued.
    /// </remarks>
    public void Cancel() {
        if (_processSession == null) {
            throw new LSException("Process not yet executed.");
        }
        _processSession.Cancel();
    }

    /// <summary>
    /// Configures or extends the processing tree for this process using a builder delegate.
    /// Allows defining custom processing logic that will be merged with global context during execution.
    /// </summary>
    /// <param name="builder">Builder action that defines the processing tree structure.</param>
    /// <param name="instance">Optional instance to use for naming the root node.</param>
    /// <returns>This process instance to enable method chaining.</returns>
    /// <remarks>
    /// <para><strong>Context Creation:</strong></para>
    /// <list type="bullet">
    /// <item><description>If no existing context, creates a new parallel root node</description></item>
    /// <item><description>If context exists, extends the existing tree structure</description></item>
    /// </list>
    /// 
    /// <para><strong>Naming Strategy:</strong></para>
    /// <para>Root node is named using the instance ID (if provided) or this process's ID,</para>
    /// <para>prefixed with the process type name for clarity in debugging.</para>
    /// </remarks>
    public ILSProcess WithProcessing(LSProcessBuilderAction builder) {
        LSProcessTreeBuilder processBuilder;
        if (_localProcessBuilder == null) {
            // no existing builder; start a new parallel node with the process type as name.
            // creating a parallel node should allow to use handlers in the builder directly.
            // the processBuilder will be merged on globalBuilder.
            // if an instance is provided, we use its ID as name for the root node.

            processBuilder = new LSProcessTreeBuilder().Parallel($"{GetType().Name}");
        } else {
            processBuilder = new LSProcessTreeBuilder(_localProcessBuilder);
        }
        // use the builder to modify or extend the processBuilder.
        _localProcessBuilder = builder(processBuilder).Build();
        return this;
    }

    /// <summary>
    /// Forces transition from WAITING to FAILURE state for the specified node IDs.
    /// </summary>
    /// <param name="nodeIDs">Array of specific node IDs to fail. If empty, fails all waiting nodes.</param>
    /// <returns>The processing status after failure operation.</returns>
    /// <exception cref="LSException">Thrown if the process has not been executed yet.</exception>
    /// <remarks>
    /// Used for timeout handling, error conditions, or explicit failure injection.
    /// May trigger cascading status changes in parent nodes based on their aggregation logic.
    /// </remarks>
    public LSProcessResultStatus Fail(params string[] nodeIDs) {
        if (_processSession == null) {
            throw new LSException("Process not yet executed.");
        }
        return _processSession.Fail(nodeIDs);
    }

    /// <summary>
    /// Associates data with this process using a string key.
    /// Allows storing information that persists for the lifetime of the process.
    /// </summary>
    /// <typeparam name="T">The type of data to store.</typeparam>
    /// <param name="key">The unique key to store the data under.</param>
    /// <param name="value">The data value to store.</param>
    /// <remarks>
    /// If a value already exists for the specified key, it will be replaced.
    /// The data is available to all handlers processing this instance.
    /// </remarks>
    public virtual void SetData<T>(string key, T value) {
        _data[key] = value!;
    }

    /// <summary>
    /// Attempts to retrieve strongly-typed data associated with the specified key.
    /// Provides safe access without throwing exceptions for missing keys or type mismatches.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <param name="value">When this method returns, contains the retrieved value if successful, or the default value for T if unsuccessful.</param>
    /// <returns>true if the data was found and successfully cast to the specified type; otherwise, false.</returns>
    public virtual bool TryGetData<T>(string key, out T value) {
        if (_data.TryGetValue(key, out var objValue)) {
            if (objValue is T tValue) {
                value = tValue;
                return true;
            }
        }
        value = default!;
        return false;
    }
}
