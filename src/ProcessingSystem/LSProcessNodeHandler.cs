namespace LSUtils.Processing;
using System.Linq;
/// <summary>
/// Leaf node implementation that executes process handler delegates in the LSProcessing system hierarchy.
/// Represents the concrete processing units that perform actual business logic within the processing pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <b>Handler Node Architecture:</b><br/>
/// This class serves as the terminal execution point in the LSProcessing system's composite hierarchy,
/// implementing the leaf node pattern where actual business logic is executed through handler delegates.
/// It provides the bridge between the abstract processing tree and concrete business operations.
/// </para>
/// <para>
/// <b>Design Patterns:</b><br/>
/// • Leaf Node: Terminal node in the Composite Pattern that executes actual logic<br/>
/// • Strategy Pattern: Encapsulates handler logic through LSProcessHandler delegate<br/>
/// • Flyweight Pattern: Shares execution count across clones through base node reference<br/>
/// • Template Method: Implements standard processing lifecycle with customizable handler logic
/// </para>
/// <para>
/// <b>Execution Semantics:</b><br/>
/// • Single Execution: Terminal states (SUCCESS, FAILURE, CANCELLED) are cached to prevent re-execution<br/>
/// • Waiting Continuation: WAITING nodes can be re-processed via Resume/Fail operations<br/>
/// • Execution Counting: Tracks handler invocations across all clones for analytics<br/>
/// • Conditional Evaluation: Respects node conditions before allowing handler execution
/// </para>
/// <para>
/// <b>State Management:</b><br/>
/// • Status Caching: Terminal statuses are cached to ensure idempotency<br/>
/// • Independent Processing: Each clone maintains its own processing state<br/>
/// • Shared Statistics: Execution count is shared among clones through base node reference<br/>
/// • Inversion Support: WithInverter property allows success/failure logic inversion
/// </para>
/// <para>
/// <b>Cloning Strategy:</b><br/>
/// When cloned, new instances reference the original node as a base node to share execution count
/// while maintaining independent processing state. This allows global execution tracking while
/// supporting parallel processing scenarios and concurrent handler execution.
/// </para>
/// <para>
/// <b>Integration Benefits:</b><br/>
/// • Composability: Seamlessly integrates with layer nodes in processing hierarchies<br/>
/// • Testability: Individual handlers can be tested in isolation from the processing tree<br/>
/// • Reusability: Handler logic can be reused across different processing contexts<br/>
/// • Monitoring: Built-in execution tracking for performance analysis and debugging
/// </para>
/// </remarks>
public class LSProcessNodeHandler : ILSProcessNode {
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
    /// <inheritdoc />
    public string NodeID { get; }
    /// <inheritdoc />
    public LSProcessPriority Priority { get; }
    /// <inheritdoc />
    public LSProcessNodeCondition? Conditions { get; }
    /// <summary>
    /// Gets a value indicating whether this handler node inverts success/failure logic.
    /// When true, SUCCESS results from the handler are treated as FAILURE and vice versa.
    /// </summary>
    /// <value>
    /// True if the node inverts success/failure logic; false for normal behavior.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property enables logical inversion patterns where the desired outcome is the opposite
    /// of what the handler naturally returns. Common use cases include:
    /// </para>
    /// <para>
    /// • Validation handlers where failure indicates success (e.g., "user is not banned")<br/>
    /// • Condition checks where false results should proceed (e.g., "cache miss" handlers)<br/>
    /// • Error detection where finding no errors is the success condition
    /// </para>
    /// </remarks>
    public bool WithInverter { get; } = false;
    /// <summary>
    /// Gets the effective SUCCESS status considering the WithInverter property.
    /// Returns FAILURE when WithInverter is true, SUCCESS otherwise.
    /// </summary>
    protected LSProcessResultStatus _nodeSuccess => WithInverter ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS;
    /// <summary>
    /// Gets the effective FAILURE status considering the WithInverter property.
    /// Returns SUCCESS when WithInverter is true, FAILURE otherwise.
    /// </summary>
    protected LSProcessResultStatus _nodeFailure => WithInverter ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
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
    protected LSProcessNodeHandler(string nodeID, LSProcessHandler handler,
                      int order, LSProcessPriority priority = LSProcessPriority.NORMAL, LSProcessNodeHandler? baseNode = null, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        _baseNode = baseNode;
        NodeID = nodeID;
        Priority = priority;
        _handler = handler;
        Order = order;
        WithInverter = withInverter;
        Conditions = LSProcessConditions.UpdateConditions(true, null, conditions);
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
    LSProcessResultStatus ILSProcessNode.Execute(LSProcessSession session) {
        // Terminal state check - return cached status for completed nodes
        if (_nodeStatus != LSProcessResultStatus.UNKNOWN && _nodeStatus != LSProcessResultStatus.WAITING) {
            return _nodeStatus; // Already completed with terminal status
        }
        // Execute the handler delegate with current process and session context
        var nodeStatus = _handler(session.Process, session);
        // Increment execution count for analytics (shared across clones via base node)
        ExecutionCount++;
        // Apply inversion logic if WithInverter is enabled
        if (nodeStatus == LSProcessResultStatus.SUCCESS)
            nodeStatus = _nodeSuccess;
        else if (nodeStatus == LSProcessResultStatus.FAILURE)
            nodeStatus = _nodeFailure;
        // WAITING and CANCELLED statuses are not inverted
        // Only update _nodeStatus if it was UNKNOWN (preserves Resume/Fail operations)
        if (_nodeStatus == LSProcessResultStatus.UNKNOWN)
            _nodeStatus = nodeStatus;

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
    LSProcessResultStatus ILSProcessNode.Resume(LSProcessSession context, params string[]? nodes) {
        // Can only resume from WAITING or UNKNOWN states
        if (_nodeStatus != LSProcessResultStatus.WAITING && _nodeStatus != LSProcessResultStatus.UNKNOWN) {
            return _nodeStatus; // Cannot resume from terminal states
        }

        // Transition to SUCCESS status (external operation completed successfully)
        _nodeStatus = LSProcessResultStatus.SUCCESS;
        return _nodeStatus;
    }
    /// <summary>
    /// Forces this handler node to transition from WAITING state to FAILURE.
    /// Used when external operations fail or timeout, requiring the handler to fail.
    /// </summary>
    /// <param name="context">The processing context (not used by handler nodes).</param>
    /// <param name="nodes">Optional node ID array for targeting (not used by handler nodes as they are leaf nodes).</param>
    /// <returns>The new processing status after failure operation.</returns>
    /// <remarks>
    /// <para><strong>Valid Transitions:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>WAITING → FAILURE</strong>: Normal failure path</description></item>
    /// <item><description><strong>UNKNOWN → FAILURE</strong>: Immediate failure without processing</description></item>
    /// <item><description><strong>Terminal States</strong>: Returns current status without change</description></item>
    /// </list>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Timeout Handling</strong>: Force failure when operations take too long</description></item>
    /// <item><description><strong>Error Propagation</strong>: Propagate external errors to waiting handlers</description></item>
    /// <item><description><strong>Resource Failure</strong>: Fail when required resources become unavailable</description></item>
    /// </list>
    /// </remarks>
    LSProcessResultStatus ILSProcessNode.Fail(LSProcessSession context, params string[]? nodes) {
        // Can only fail from WAITING or UNKNOWN states  
        if (_nodeStatus != LSProcessResultStatus.WAITING && _nodeStatus != LSProcessResultStatus.UNKNOWN) {
            return _nodeStatus; // Cannot fail from terminal states
        }

        // Transition to FAILURE status (external operation failed or timed out)
        _nodeStatus = LSProcessResultStatus.FAILURE;
        return _nodeStatus;
    }
    /// <summary>
    /// Cancels processing for this handler node, setting its status to CANCELLED.
    /// This operation is always successful and provides a terminal state.
    /// </summary>
    /// <param name="context">The processing context (not used by handler nodes).</param>
    /// <returns>CANCELLED status after the operation.</returns>
    /// <remarks>
    /// <para><strong>Finality:</strong> CANCELLED is a terminal state that cannot be resumed or changed.</para>
    /// <para><strong>Idempotency:</strong> Safe to call multiple times, always results in CANCELLED status.</para>
    /// <para><strong>Scope:</strong> Only affects this individual handler node (leaf nodes have no children).</para>
    /// </remarks>
    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession context) {
        // Set status to CANCELLED (terminal state, always succeeds)
        _nodeStatus = LSProcessResultStatus.CANCELLED;
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
        return new LSProcessNodeHandler(NodeID, _handler, Order, Priority, this, WithInverter, Conditions);
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
                      int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeHandler(nodeID, handler, order, priority, null, withInverter, conditions);
    }
}
