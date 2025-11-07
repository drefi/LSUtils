namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

/// <summary>
/// Base implementation of ILSProcess providing data storage, execution orchestration, and session management.
/// 
/// LSProcess serves as a data container that flows through processing pipelines, carrying state
/// between handlers while the execution logic resides in the node hierarchy. This separation
/// enables flexible workflow design where the same process type can have different behaviors
/// based on registered contexts.
/// 
/// Architecture:
/// - Process = Data container with minimal behavior
/// - LSProcessSession = Execution state management and node coordination
/// - ILSProcessLayerNode hierarchy = Actual processing logic and flow control
/// 
/// Implementation Details:
/// - Single execution model: Execute() can only be called once per instance
/// - Lazy session creation: Session created only when Execute() is called
/// - Status delegation: IsCancelled/IsCompleted delegate to session root node status
/// - Context merging: Local _root merges with registered contexts during execution
/// - Data isolation: Each process has independent dictionary for inter-handler communication
/// 
/// Typical Subclass Pattern:
/// ```csharp
/// public class MyCustomProcess : LSProcess {
///     public string ProcessSpecificData { get; set; }
///     // No need to override Execute() - base class handles orchestration
/// }
/// ```
/// </summary>
public abstract class LSProcess {
    public const string ClassName = nameof(LSProcess);
    /// <summary>
    /// Execution session created when Execute() is called. Manages actual processing state
    /// and coordinates with the node hierarchy. Remains null until first execution.
    /// </summary>
    private LSProcessSession? _processSession;

    /// <summary>
    /// Internal key-value store for inter-handler communication during processing.
    /// Isolated per process instance and persists throughout execution lifecycle.
    /// </summary>
    private Dictionary<string, object> _data = new();

    /// <summary>
    /// Local processing tree defined via WithProcessing(). Merged with registered
    /// contexts during execution, with local context taking highest priority.
    /// </summary>
    private ILSProcessLayerNode? _root;

    /// <summary>
    /// Reference to the process manager used for execution. Cached from Execute() call.
    /// </summary>
    private LSProcessManager? _manager;

    /// <summary>
    /// Auto-generated unique identifier assigned at construction.
    /// Used for debugging, logging, and session association.
    /// </summary>
    public System.Guid ID { get; }

    /// <summary>
    /// UTC timestamp of process creation for timing analysis and audit trails.
    /// Set once at construction and never changes.
    /// </summary>
    public System.DateTime CreatedAt { get; }
    /// <summary>
    /// Delegates to the session's root node status to determine cancellation state.
    /// Returns false if no execution session exists (process not yet executed).
    /// </summary>
    public bool IsCancelled {
        get {
            if (_processSession == null) return false;
            return _processSession.RootNode.GetNodeStatus() == LSProcessResultStatus.CANCELLED;
        }
    }

    /// <summary>
    /// Determines completion by checking if the session's root node has reached any terminal state.
    /// Returns false if no execution session exists (process not yet executed).
    /// Terminal states: SUCCESS, FAILURE, CANCELLED
    /// Non-terminal states: UNKNOWN, WAITING
    /// </summary>
    public bool IsCompleted {
        get {
            if (_processSession == null) return false;
            var status = _processSession.RootNode.GetNodeStatus();
            return status == LSProcessResultStatus.SUCCESS || status == LSProcessResultStatus.FAILURE || status == LSProcessResultStatus.CANCELLED;
        }
    }
    /// <summary>
    /// Protected constructor enforcing abstract class pattern. Concrete process types
    /// should inherit from this class and add domain-specific properties/methods.
    /// 
    /// Automatically generates unique ID and creation timestamp for tracking purposes.
    /// </summary>
    protected LSProcess() {
        ID = System.Guid.NewGuid();
        CreatedAt = System.DateTime.UtcNow;
    }
    protected LSProcess(IReadOnlyDictionary<string, object> data) : this() {
        _data = new Dictionary<string, object>(data);
    }

    /// <summary>
    /// Virtual method that allows concrete process classes to define their processing tree through inheritance.
    /// This method is called during Execute() and takes precedence over WithProcessing() configuration.
    /// </summary>
    /// <param name="builder">The tree builder to configure the processing hierarchy.</param>
    /// <param name="layerType">The root node type for the processing tree.</param>
    /// <returns>The configured root node for this process type, or null to use WithProcessing() configuration.</returns>
    /// <remarks>
    /// <para><strong>Override Pattern:</strong></para>
    /// <para>Concrete classes can override this method to define their processing logic declaratively:</para>
    /// <code>
    /// protected override ILSProcessLayerNode? DefineProcessing(LSProcessTreeBuilder builder, LSProcessLayerNodeType layerType) {
    ///     return builder.Sequence("my-process", seq => seq
    ///         .Handler("validate", ValidateInput)
    ///         .Handler("process", ProcessLogic)
    ///         .Handler("cleanup", Cleanup))
    ///     .Build();
    /// }
    /// </code>
    /// <para><strong>Execution Priority:</strong></para>
    /// <para>If this method returns a non-null value, it takes precedence over WithProcessing() configuration.</para>
    /// <para>This allows subclasses to have built-in processing logic while still supporting runtime customization.</para>
    /// </remarks>
    protected virtual LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder;
    }

    public LSProcessResultStatus Execute(params ILSProcessable[]? instances) {
        return Execute(LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL, instances);
    }
    /// <summary>
    /// Executes the process through the registered processing pipeline (single-use operation).
    /// 
    /// Creates LSProcessSession, retrieves merged context from LSProcessManager, and delegates
    /// execution to the session. Subsequent calls return cached status from first execution.
    /// 
    /// Execution Flow:
    /// 1. Check if already executed (return cached status if so)
    /// 2. Get merged root node from manager (local + instance + global contexts)
    /// 3. Create execution session with this process as data container
    /// 4. Delegate to session.Execute() for actual processing
    /// 
    /// The process serves as a passive data container while the session manages execution state.
    /// </summary>
    /// <param name="instance">Target entity for context resolution (may be null for global context)</param>
    /// <param name="manager">Process manager for context merging (uses singleton if null)</param>
    /// <returns>Final execution status (may be WAITING if contains async operations)</returns>
    public LSProcessResultStatus Execute(LSProcessManager manager, LSProcessManager.ProcessInstanceBehaviour instanceBehaviour, params ILSProcessable[]? instances) {
        // Flow debug logging LSProcessSystem
        LSLogger.Singleton.Debug($"{ClassName}.Execute: [{_root?.NodeID ?? "n/a"}] instance: {(instances == null ? "n/a" : $"{string.Join(", ", instances.Select(i => i.ID))}")}.",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        if (manager == null) throw new LSException("Process manager cannot be null.");
        _manager = manager;
        if (_processSession != null) {
            //log warning
            LSLogger.Singleton.Warning($"Process already executed. Returning current status.",
                source: (ClassName, null),
                processId: ID,
                properties: new (string, object)[] {
                    ("session", _processSession.SessionID.ToString()),
                    ("rootNode", _processSession.RootNode.NodeID.ToString()),
                    ("currentNode", _processSession.CurrentNode?.NodeID.ToString() ?? "null"),
                    ("instances", _processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}"),
                    ("method", nameof(Execute))
                });
            return _processSession.RootNode.GetNodeStatus();
        }

        ILSProcessLayerNode? localRoot = processing(new LSProcessTreeBuilder(_root)).Build();


        var sessionRoot = _manager.GetRootNode(GetType(), localRoot, out var availableInstances, instanceBehaviour, instances);
        _processSession = new LSProcessSession(_manager, this, sessionRoot, availableInstances);

        // Detailed debug logging ClassName
        LSLogger.Singleton.Debug($"Process Execute",
              source: (ClassName, null),
              processId: ID,
              properties: new (string, object)[] {
                ("session", _processSession.SessionID.ToString()),
                ("rootNode", _processSession.RootNode.NodeID.ToString()),
                ("currentNode", _processSession.CurrentNode?.NodeID.ToString() ?? "null"),
                ("behaviour", _processSession.Behaviour.ToString()),
                ("instances", _processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}"),
                ("method", nameof(Execute))
            });
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
        // Flow debug logging
        LSLogger.Singleton.Debug($"LSProcess.Resume [{_processSession.RootNode.NodeID}] {_processSession.Behaviour} {(_processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}")}.",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        LSLogger.Singleton.Debug($"Process Resume",
              source: (ClassName, null),
              processId: ID,
              properties: new (string, object)[] {
                ("session", _processSession.SessionID.ToString()),
                ("rootNode", _processSession.RootNode.NodeID.ToString()),
                ("currentNode", _processSession.CurrentNode?.NodeID.ToString() ?? "null"),
                ("behaviour", _processSession.Behaviour.ToString()),
                ("instance", _processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}"),
                ("nodes", string.Join(",", nodeIDs)),
                ("method", nameof(Resume))
            });
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
        // Flow debug logging
        LSLogger.Singleton.Debug($"LSProcess.Cancel [{_processSession.RootNode.NodeID}] {_processSession.Behaviour} {(_processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}")}.",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        LSLogger.Singleton.Debug($"Process Cancel",
              source: (ClassName, null),
              processId: ID,
              properties: new (string, object)[] {
                ("session", _processSession.SessionID.ToString()),
                ("rootNode", _processSession.RootNode.NodeID.ToString()),
                ("currentNode", _processSession.CurrentNode?.NodeID.ToString() ?? "null"),
                ("behaviour", _processSession.Behaviour.ToString()),
                ("instances", _processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}"),
                ("method", nameof(Cancel))
            });
        _processSession.Cancel();
    }
    /// <summary>
    /// Configures or extends the processing tree for this process using a builder delegate.
    /// Allows defining custom processing logic that will be merged with global context during execution.
    /// </summary>
    /// <param name="builderAction">Builder action that defines the processing tree structure.</param>
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
    public LSProcess WithProcessing(LSProcessBuilderAction builderAction, LSProcessLayerNodeType layerType = LSProcessLayerNodeType.SELECTOR) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.WithProcessing: [{_root?.NodeID ?? "n/a"}] {_root?.GetType().Name ?? layerType.ToString()} ",
            source: ("LSProcessSystem", null),
            processId: ID,
            properties: ("hideNodeID", true));

        LSProcessTreeBuilder builder;
        if (_root == null) {
            builder = layerType switch {
                LSProcessLayerNodeType.SEQUENCE => new LSProcessTreeBuilder().Sequence($"{GetType().Name}"),
                LSProcessLayerNodeType.SELECTOR => new LSProcessTreeBuilder().Selector($"{GetType().Name}"),
                _ => new LSProcessTreeBuilder().Parallel($"{GetType().Name}"),
            };
        } else {
            builder = new LSProcessTreeBuilder(_root);
        }
        // use the builderAction to modify or extend the root.
        _root = builderAction(builder).Build();

        LSLogger.Singleton.Debug($"Modifying Process Tree",
              source: (ClassName, null),
              processId: ID,
              properties: new (string, object)[] {
                ("rootNode", _root.NodeID.ToString()),
                ("layerType", layerType.ToString()),
                ("method", nameof(WithProcessing))
            });
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
        // Flow debug logging
        LSLogger.Singleton.Debug("LSProcess.Fail",
              source: ("LSProcessSystem", null),
              processId: ID);

        if (_processSession == null) {
            throw new LSException("Process not yet executed.");
        }
        LSLogger.Singleton.Debug($"Process Fail",
              source: (ClassName, null),
              processId: ID,
              properties: new (string, object)[] {
                ("session", _processSession.SessionID.ToString()),
                ("rootNode", _processSession.RootNode.NodeID.ToString()),
                ("currentNode", _processSession.CurrentNode?.NodeID.ToString() ?? "null"),
                ("behaviour", _processSession.Behaviour.ToString()),
                ("instances", _processSession.Instances == null ? "n/a" : $"{string.Join(", ", _processSession.Instances.Select(i => i.ID))}"),
                ("nodes", string.Join(",", nodeIDs)),
                ("method", nameof(Fail))
            });
        return _processSession.Fail(nodeIDs);
    }
    /// <summary>
    /// Exception-throwing data retrieval from the internal dictionary.
    /// Validates both key existence and type compatibility before returning.
    /// Virtual to allow subclasses to override data access behavior if needed.
    /// </summary>
    /// <typeparam name="T">Expected data type</typeparam>
    /// <param name="key">Case-sensitive string key</param>
    /// <returns>Stored value cast to type T</returns>
    /// <exception cref="KeyNotFoundException">Key not found in internal dictionary</exception>
    /// <exception cref="LSException">Value exists but cannot be cast to T</exception>
    public virtual T? GetData<T>(string key) {
        if (_data.TryGetValue(key, out var value)) {
            if (value is T tValue) {
                return tValue;
            }
            throw new LSException($"Stored data with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"No data found for key '{key}'.");
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
    public virtual bool TryGetData<T>(string key, out T value, bool? enableLogging = false) {
        string objValueType = "n/a";
        if (_data.TryGetValue(key, out var objValue)) {
            objValueType = $"{objValue.GetType().Name}";
            if (objValue is T tValue) {
                value = tValue;
                return true;
            }
        }
        LSLogger.Singleton.Debug($"Failed to retrieve data",
            source: (ClassName, enableLogging),
            processId: ID,
            properties: new (string, object)[] {
                ("key", key),
                ("expectedType", typeof(T).Name),
                ("objValueType", objValueType),
                ("method", nameof(TryGetData))
            });
        value = default(T)!;
        return false;
    }
}
