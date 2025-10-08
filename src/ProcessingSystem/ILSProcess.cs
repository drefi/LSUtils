namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
/// <summary>
/// Core process interface representing an immutable data container with comprehensive state tracking.
/// Processes serve as data carriers throughout the processing pipeline, providing access to 
/// process metadata, processing state information, and associated data while maintaining clean 
/// separation between process data and processing logic.
/// 
/// Processes are designed to be self-contained units of work that can be processed through 
/// multiple phases without requiring external state management. All state properties are 
/// read-only from the handler perspective to ensure immutability.
/// </summary>
public interface ILSProcess {
    /// <summary>
    /// Unique identifier for this process instance.
    /// Generated automatically when the process is created and remains constant throughout
    /// the process's entire lifecycle.
    /// </summary>
    System.Guid ID { get; }

    /// <summary>
    /// UTC timestamp when this process was created.
    /// Provides timing information for process execution analytics, debugging, and audit trails.
    /// </summary>
    System.DateTime CreatedAt { get; }

    /// <summary>
    /// Indicates whether the process execution was cancelled by a handler.
    /// When true, no further phase processing will occur.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Indicates whether the process has completed execution through all phases.
    /// A process is considered completed when the processing lifecycle is finished.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Associates data with this process using a string key.
    /// Allows handlers to store information that persists for the lifetime of the process
    /// and can be accessed by subsequent handlers in the processing pipeline.
    /// </summary>
    /// <param name="key">The unique key to store the data under.</param>
    /// <param name="value">The data value to store.</param>
    void SetData<T>(string key, T value);

    /// <summary>
    /// Retrieves strongly-typed data associated with this process.
    /// Provides type-safe access to stored process data with compile-time type checking.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored data cannot be cast to the specified type.</exception>
    T? GetData<T>(string key);

    /// <summary>
    /// Attempts to retrieve strongly-typed data associated with this process.
    /// Provides safe access to stored process data without throwing exceptions for missing keys or type mismatches.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored data.</typeparam>
    /// <param name="key">The key used to store the data.</param>
    /// <param name="value">When this method returns, contains the retrieved value if successful, or the default value for T if unsuccessful.</param>
    /// <returns>true if the data was found and successfully cast to the specified type; otherwise, false.</returns>
    bool TryGetData<T>(string key, out T? value);

    /// <summary>
    /// Configures or extends the processing tree for this process using a builder action.
    /// This will be merged with the root context before processing.
    /// <see cref="LSProcessManager"/> root context is a parallel root node with no number.
    /// Returns the process instance to allow chaining.
    /// </summary>
    /// <param name="builder">Process tree builder action that defines the processing hierarchy.</param>
    /// <param name="instance">Optional instance to associate with the process context.</param>
    /// <returns>This process instance to enable method chaining.</returns>
    /// <remarks>
    /// <para><strong>Context Merging:</strong></para>
    /// <list type="bullet">
    /// <item><description>Custom processing trees are merged with global context during execution</description></item>
    /// <item><description>Allows process-specific workflow customization while maintaining system consistency</description></item>
    /// <item><description>Can be called multiple times to build complex hierarchies incrementally</description></item>
    /// </list>
    /// </remarks>
    ILSProcess WithProcessing(LSProcessBuilderAction builder, LSProcessLayerNodeType layerType = LSProcessLayerNodeType.PARALLEL);

    /// <summary>
    /// Execute the process through a context manager (or Singleton if not provided).
    /// Creates an execution session and processes through the node hierarchy.
    /// </summary>
    /// <param name="instance">Optional processable instance to use as context target for execution.</param>
    /// <param name="contextManager">Optional context manager to use for processing. Uses singleton if not provided.</param>
    /// <returns>The result status of the process execution (SUCCESS, FAILURE, WAITING, CANCELLED).</returns>
    /// <remarks>
    /// <para><strong>Execution Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Single Execution</strong>: Can only be called once per process instance</description></item>
    /// <item><description><strong>Context Merging</strong>: Combines custom processing trees with global context</description></item>
    /// <item><description><strong>Session Management</strong>: Creates internal session for state tracking</description></item>
    /// <item><description><strong>Asynchronous Support</strong>: Can return WAITING status for async operations</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Execute(ILSProcessable? instance = null);

    /// <summary>
    /// Resume processing for the specified node IDs. Process context should be stored in the process instance.
    /// </summary>
    /// <param name="nodeIDs">Array of specific node IDs to resume. If empty, resumes all waiting nodes.</param>
    /// <returns>The result status after attempting to resume processing (SUCCESS, FAILURE, WAITING, CANCELLED).</returns>
    /// <remarks>
    /// <para><strong>Resumption Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Targeted Resume</strong>: When node IDs provided, only those nodes are resumed</description></item>
    /// <item><description><strong>Global Resume</strong>: When no IDs provided, all waiting nodes are resumed</description></item>
    /// <item><description><strong>State Validation</strong>: Only WAITING nodes can be resumed</description></item>
    /// <item><description><strong>Cascading Effects</strong>: May trigger parent node status changes</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Resume(params string[] nodeIDs);

    /// <summary>
    /// Fail processing for the specified node IDs. Process context should be stored in the process instance.
    /// </summary>
    /// <param name="nodeIDs">Array of specific node IDs to fail. If empty, fails all waiting nodes.</param>
    /// <returns>The result status after forcing failure on the specified nodes (typically FAILURE).</returns>
    /// <remarks>
    /// <para><strong>Failure Injection:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Targeted Failure</strong>: When node IDs provided, only those nodes are failed</description></item>
    /// <item><description><strong>Global Failure</strong>: When no IDs provided, all waiting nodes are failed</description></item>
    /// <item><description><strong>Use Cases</strong>: Timeout handling, error conditions, resource constraints</description></item>
    /// <item><description><strong>Cascading Effects</strong>: May trigger parent node status changes based on aggregation logic</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus Fail(params string[] nodeIDs);

    /// <summary>
    /// Cancels the entire process execution immediately.
    /// Sets the process to CANCELLED state and prevents any further processing.
    /// </summary>
    /// <remarks>
    /// <para>This is a terminal operation that cannot be undone. Once cancelled, the process</para>
    /// <para>cannot be resumed or continued through any means.</para>
    /// </remarks>
    void Cancel();
}
