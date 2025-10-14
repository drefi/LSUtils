namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

/// <summary>
/// Terminal execution node that wraps LSProcessHandler delegates within the processing hierarchy.
/// <para>
/// LSProcessNodeHandler serves as the leaf node in the Composite Pattern, providing the bridge
/// between the abstract processing tree structure and concrete business logic execution.
/// Each handler node encapsulates a single operation that can access process data and return
/// execution status to control processing flow.
/// </para>
/// <para>
/// <b>Execution Model:</b><br/>
/// - Single-shot execution for terminal states (SUCCESS, FAILURE, CANCELLED)<br/>
/// - Re-execution allowed for WAITING states via Resume/Fail operations<br/>
/// - Condition evaluation before handler invocation<br/>
/// - Shared execution counting across clones for analytics
/// </para>
/// <para>
/// <b>Handler Delegate Signature:</b><br/>
/// LSProcessHandler receives LSProcessSession containing the process data and execution context,
/// allowing handlers to access/modify process state and return appropriate status codes.
/// </para>
/// <para>
/// <b>Cloning Behavior:</b><br/>
/// Cloned nodes maintain independent processing state while sharing execution count
/// through _baseNode reference, enabling parallel processing with centralized statistics.
/// </para>
/// </summary>
/// <example>
/// Creating and using handler nodes:
/// <code>
/// // Handler that validates input data
/// LSProcessHandler validateInput = (session) => {
///     var input = session.Process.TryGetData&lt;string&gt;("input", out var value) ? value : "";
///     if (string.IsNullOrEmpty(input)) {
///         session.Process.SetData("error", "Input is required");
///         return LSProcessResultStatus.FAILURE;
///     }
///     return LSProcessResultStatus.SUCCESS;
/// };
/// 
/// // Handler that waits for external completion
/// LSProcessHandler waitForCallback = (session) => {
///     // Register for external callback
///     RegisterExternalCallback(session.Process.ID);
///     return LSProcessResultStatus.WAITING; // Will be resumed later
/// };
/// 
/// // Used in builder pattern
/// builder.Handler("validate-input", validateInput)
///        .Handler("wait-for-callback", waitForCallback);
/// </code>
/// </example>
public class LSProcessNodeHandler : ILSProcessNode {
    public const string ClassName = nameof(LSProcessNodeHandler);
    /// <summary>
    /// Reference to the original node for sharing execution count across clones.
    /// Used to implement shared execution statistics while maintaining independent processing state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a node is cloned, the new instance references the original as its base node.
    /// This allows execution count to be shared globally while each clone maintains its own processing status,
    /// enabling accurate analytics across parallel processing scenarios.
    /// </para>
    /// </remarks>
    protected LSProcessNodeHandler? _baseNode;
    /// <summary>
    /// Local execution count for this specific node instance.
    /// Used when this node is not a clone (no base node reference).
    /// </summary>
    /// <remarks>
    /// This field stores the execution count for original (non-clone) nodes. When the node is cloned,
    /// the clones will reference the original's execution count through the base node reference.
    /// </remarks>
    protected int _executionCount = 0;
    /// <summary>
    /// The handler delegate that contains the actual business logic to be executed.
    /// This delegate is invoked when the node is processed and determines the processing outcome.
    /// </summary>
    /// <remarks>
    /// The handler receives both the current process and the processing session context,
    /// allowing it to access process data and node metadata for decision-making.
    /// </remarks>
    protected LSProcessHandler _handler;
    protected bool _hasExecuted = false;
    /// <inheritdoc />
    public string NodeID { get; }
    /// <inheritdoc />
    public LSProcessPriority Priority { get; }
    /// <inheritdoc />
    public LSProcessNodeCondition? Conditions { get; }
    /// <summary>
    /// Gets or sets the execution count, automatically delegating to the base node if this is a clone.
    /// Provides shared execution statistics across all clones of a handler node.
    /// </summary>
    /// <value>
    /// The total number of times this handler (including all clones) has been executed.
    /// When accessed on a clone, returns the base node's execution count.
    /// When set on a clone, updates the base node's execution count.
    /// </value>
    /// <remarks>
    /// This property implements shared state across clones while maintaining independence
    /// of other processing aspects. The execution count is incremented each time the
    /// handler delegate is invoked, regardless of the processing outcome.
    /// </remarks>
    public int ExecutionCount {
        get => _baseNode?.ExecutionCount ?? _executionCount;
        internal set {
            if (_baseNode != null) {
                _baseNode.ExecutionCount = value;
            } else {
                _executionCount = value;
            }
        }
    }
    /// <inheritdoc />
    /// <remarks>
    /// Order is set during registration in the processing system, starts at 0 and increases 
    /// with each new node registered. Used for deterministic execution ordering among 
    /// sibling nodes with the same priority level.
    /// </remarks>
    public int Order { get; }
    public bool ReadOnly { get; } = false;

    /// <summary>
    /// Initializes a new handler node with the specified configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for this node within its parent scope.</param>
    /// <param name="handler">The handler delegate to execute when this node is processed.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="baseNode">Base node reference for sharing execution count (used during cloning).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the handler node.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <para><strong>Condition Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Default</strong>: If no conditions provided, uses a default condition that always returns true</description></item>
    /// <item><description><strong>Composition</strong>: Multiple conditions are combined using delegate composition (+=)</description></item>
    /// <item><description><strong>Null Safety</strong>: Null conditions in the array are automatically filtered out</description></item>
    /// </list>
    /// 
    /// <para><strong>Base Node Pattern:</strong></para>
    /// <para>When baseNode is provided, this instance becomes a clone that shares execution count</para>
    /// <para>with the original node while maintaining independent processing state.</para>
    /// </remarks>
    protected LSProcessNodeHandler(string nodeID,
            LSProcessHandler handler,
            int order,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            LSProcessNodeHandler? baseNode = null,
            bool readOnly = false,
            params LSProcessNodeCondition?[] conditions) {
        _baseNode = baseNode;
        NodeID = nodeID;
        Priority = priority;
        _handler = handler;
        Order = order;
        ReadOnly = readOnly;
        //WithInverter = withInverter;
        Conditions = LSProcessHelpers.UpdateConditions(true, null, conditions);
    }
    /// <inheritdoc />
    public LSProcessResultStatus GetNodeStatus() {
        return _nodeStatus;
    }
    /// <summary>
    /// Current processing status of this handler node.
    /// Cached after first execution to ensure idempotency for terminal states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field implements single-execution semantics for terminal states while allowing 
    /// re-processing for WAITING states through Resume/Fail operations. The status progression follows:
    /// </para>
    /// <para>
    /// UNKNOWN → [Handler Execution] → SUCCESS/FAILURE/WAITING/CANCELLED<br/>
    /// WAITING → [Resume/Fail] → SUCCESS/FAILURE<br/>
    /// Terminal states (SUCCESS/FAILURE/CANCELLED) are cached and prevent re-execution
    /// </para>
    /// </remarks>
    LSProcessResultStatus _nodeStatus = LSProcessResultStatus.UNKNOWN;
    /// <summary>
    /// Processes this handler node by executing its associated handler delegate.
    /// Implements single-execution semantics for terminal states while allowing re-processing for WAITING states.
    /// </summary>
    /// <param name="session">The processing session containing the current process and system state.</param>
    /// <returns>The processing status after handler execution.</returns>
    /// <remarks>
    /// <para>
    /// <b>Execution Logic:</b><br/>
    /// 1. Terminal State Check: Returns cached status if already in SUCCESS, FAILURE, or CANCELLED state<br/>
    /// 2. Handler Invocation: Calls the associated handler delegate with process and session context<br/>
    /// 3. Result Processing: Applies inversion logic if WithInverter is enabled<br/>
    /// 4. Execution Counting: Increments execution count regardless of handler outcome<br/>
    /// 5. Status Caching: Caches handler result for future calls, except when transitioning from WAITING
    /// </para>
    /// <para>
    /// <b>WAITING State Handling:</b><br/>
    /// When the current status is WAITING, the handler is re-executed to allow for continuation
    /// logic. The status is only updated if it was previously UNKNOWN, preserving Resume/Fail transitions
    /// made by external operations.
    /// </para>
    /// <para>
    /// <b>Inversion Logic:</b><br/>
    /// When WithInverter is true, SUCCESS results are mapped to FAILURE and vice versa, while
    /// WAITING and CANCELLED states remain unchanged. This allows for logical inversion patterns
    /// without affecting asynchronous operation semantics.
    /// </para>
    /// <para>
    /// <b>Execution Analytics:</b><br/>
    /// The execution count is incremented on every handler invocation, providing analytics
    /// on actual handler execution frequency regardless of caching or status outcomes. This count
    /// is shared across clones for accurate system-wide statistics.
    /// </para>
    /// </remarks>
    public LSProcessResultStatus Execute(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Execute [{NodeID}]",
                source: ("LSProcessSystem", null),
                processId: session.Process.ID,
                properties: ("hideNodeID", true));

        if (_hasExecuted) {
            //log warning (handler should not run again)
            LSLogger.Singleton.Warning($"Handler node already executed.",
                source: (ClassName, true),
                processId: session.Process.ID,
                properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodeStatus", _nodeStatus),
                    ("ExecutionCount", ExecutionCount),
                    ("isClone", _baseNode != null),
                    ("method", nameof(Execute))
                });
            return _nodeStatus;
        }
        _hasExecuted = true;
        // Execute the handler delegate with current process and session context
        var handlerResult = _handler(session);
        // Increment execution count for analytics (shared across clones via base node)
        ExecutionCount++;
        // update status if handlerResult is CANCELLED
        // otherwise is SUCCESS or FAILURE because of Resume/Fail
        _nodeStatus = handlerResult == LSProcessResultStatus.CANCELLED ? handlerResult : _nodeStatus == LSProcessResultStatus.UNKNOWN ? handlerResult : _nodeStatus;
        // Debug log with details
        LSLogger.Singleton.Debug($"Handler node executed.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("handlerResult", handlerResult),
                ("nodeStatus", _nodeStatus),
                ("ExecutionCount", ExecutionCount),
                ("isClone", _baseNode != null),
                ("method", nameof(Execute))
            });
        return _nodeStatus;
    }
    /// <summary>
    /// Resumes processing for this handler node from WAITING state.
    /// Transitions the node from WAITING to SUCCESS, allowing processing to continue.
    /// </summary>
    /// <param name="context">The processing context (not used by handler nodes).</param>
    /// <param name="nodes">Optional node ID array for targeting (not used by handler nodes as they are leaf nodes).</param>
    /// <returns>The new processing status after resumption.</returns>
    /// <remarks>
    /// <para><strong>Valid Transitions:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>WAITING → SUCCESS</strong>: Normal resumption path</description></item>
    /// <item><description><strong>UNKNOWN → SUCCESS</strong>: Immediate success without processing</description></item>
    /// <item><description><strong>Terminal States</strong>: Returns current status without change</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <para>Typically used when external asynchronous operations complete successfully,</para>
    /// <para>allowing the handler to transition from WAITING to SUCCESS without re-executing the handler delegate.</para>
    /// </remarks>
    public LSProcessResultStatus Resume(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Resume [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        // execute if we never executed before, this can happen if Resume is called directly.
        if (!_hasExecuted) {
            // set _nodeStatus to SUCCESS to prevent Execute return WAITING
            _nodeStatus = LSProcessResultStatus.SUCCESS;
            // execute the handler, since handler can still cancel or fail we return the Execute result
            return Execute(session);
        }
        if (_nodeStatus != LSProcessResultStatus.WAITING && _nodeStatus != LSProcessResultStatus.UNKNOWN) {
            LSLogger.Singleton.Warning($"Handler node not waiting.",
                source: (ClassName, true),
                processId: session.Process.ID,
                properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodeStatus", _nodeStatus),
                    ("ExecutionCount", ExecutionCount),
                    ("isClone", _baseNode != null),
                    ("method", nameof(Resume))
                }
            );
            return _nodeStatus;
        }

        _nodeStatus = LSProcessResultStatus.SUCCESS;
        // Debug log with details
        LSLogger.Singleton.Debug($"Handler node resumed.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("method", nameof(Resume))
            });
        return _nodeStatus;
    }
    /// <summary>
    /// Forces this handler node to transition to FAILURE unless the node was already CANCELLED.
    /// </summary>
    /// <param name="session">The processing session.</param>
    /// <param name="nodes">Optional node ID array for targeting (not used by handler nodes as they are leaf nodes).</param>
    /// <returns>Node status FAILURE unless node already CANCELLED.</returns>
    public LSProcessResultStatus Fail(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Fail [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));

        // execute if we never executed before
        if (!_hasExecuted) {
            // pre-set to FAILURE, this prevents Execute to return WAITING
            _nodeStatus = LSProcessResultStatus.FAILURE;
            // execute the handler, since we handler can still cancel or succeed by itself we return the Execute result
            return Execute(session);
        }
        if (_nodeStatus != LSProcessResultStatus.WAITING && _nodeStatus != LSProcessResultStatus.UNKNOWN) {
            LSLogger.Singleton.Warning($"Handler node not waiting.",
                  source: (ClassName, true),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodeStatus", _nodeStatus),
                    ("ExecutionCount", ExecutionCount),
                    ("isClone", _baseNode != null),
                    ("method", nameof(Fail))
                });
            return _nodeStatus;
        }
        _nodeStatus = LSProcessResultStatus.FAILURE;
        // Debug log with details
        LSLogger.Singleton.Debug($"Handler node failed.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("method", nameof(Fail))
            });

        return _nodeStatus;
    }
    /// <summary>
    /// Cancels processing for this handler node, setting its status to CANCELLED.
    /// This operation is always successful and provides a terminal state.
    /// </summary>
    /// <param name="session">The processing context (not used by handler nodes).</param>
    /// <returns>CANCELLED status after the operation.</returns>
    /// <remarks>
    /// <para><strong>Finality:</strong> CANCELLED is a terminal state that cannot be resumed or changed.</para>
    /// <para><strong>Idempotency:</strong> Safe to call multiple times, always results in CANCELLED status.</para>
    /// <para><strong>Scope:</strong> Only affects this individual handler node (leaf nodes have no children).</para>
    /// </remarks>
    public LSProcessResultStatus Cancel(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Cancel [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));

        // prevent executing if we never executed before
        var wasExecuted = _hasExecuted;
        _hasExecuted = true;
        if (_nodeStatus == LSProcessResultStatus.CANCELLED) {
            LSLogger.Singleton.Warning($"Handler node already cancelled.",
                  source: (ClassName, true),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("executionCount", ExecutionCount),
                    ("isClone", _baseNode != null),
                    ("method", nameof(Cancel))
                });
            return _nodeStatus;
        }
        // Set handler to CANCELLED (terminal state)
        _nodeStatus = LSProcessResultStatus.CANCELLED;
        // Debug log with details
        LSLogger.Singleton.Debug($"Handler node cancelled.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("wasExecuted", wasExecuted),
                ("method", nameof(Cancel))
            });

        return _nodeStatus;
    }
    /// <summary>
    /// Creates a clone of this handler node that shares execution count with the original.
    /// The clone maintains independent processing state while sharing execution statistics.
    /// </summary>
    /// <returns>New handler node instance that references this node as its base node.</returns>
    /// <remarks>
    /// <para><strong>Shared State:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Execution Count</strong>: Shared through base node reference</description></item>
    /// <item><description><strong>Configuration</strong>: NodeID, handler, priority, order, and conditions copied</description></item>
    /// </list>
    /// 
    /// <para><strong>Independent State:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Processing Status</strong>: Clone starts with UNKNOWN status</description></item>
    /// <item><description><strong>Processing History</strong>: Independent processing state and transitions</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <para>Enables parallel processing scenarios where the same handler logic needs to be</para>
    /// <para>executed multiple times while maintaining global execution statistics.</para>
    /// </remarks>
    public ILSProcessNode Clone() {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Clone [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        var clone = new LSProcessNodeHandler(NodeID, _handler, Order, Priority, _baseNode == null ? this : _baseNode, ReadOnly, Conditions);
        // Debug log with details
        LSLogger.Singleton.Debug($"Handler node [{NodeID}] cloned.",
              source: (ClassName, null));
        return clone;
    }
    /// <summary>
    /// Factory method for creating a new handler node with the specified configuration.
    /// Provides a convenient way to create handler nodes without exposing the protected constructor.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the handler node.</param>
    /// <param name="handler">Handler delegate containing the business logic to execute.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the handler node.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>New handler node instance ready for use in the processing hierarchy.</returns>
    /// <remarks>
    /// <para>
    /// This factory method creates original (non-clone) handler nodes by passing null as the baseNode parameter.
    /// The resulting node will track its own execution count rather than sharing with another node.
    /// </para>
    /// <para>
    /// <b>Usage Pattern:</b><br/>
    /// Typically used by the fluent builder pattern and direct node creation scenarios where a new
    /// independent handler node is needed within the processing hierarchy.
    /// </para>
    /// </remarks>
    public static LSProcessNodeHandler Create(string nodeID, LSProcessHandler handler,
                      int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeHandler(nodeID, handler, order, priority, null, readOnly, conditions);
    }
}
