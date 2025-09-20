using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Leaf node implementation that executes event handler delegates in the LSEventSystem v5 hierarchy.
/// Represents the concrete processing units that perform actual business logic.
/// </summary>
/// <remarks>
/// <para><strong>Design Patterns:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Leaf Node</strong>: Terminal node in the Composite Pattern that executes actual logic</description></item>
/// <item><description><strong>Strategy Pattern</strong>: Encapsulates handler logic through LSEventHandler delegate</description></item>
/// <item><description><strong>Flyweight Pattern</strong>: Shares execution count across clones through base node reference</description></item>
/// </list>
/// 
/// <para><strong>Execution Semantics:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Single Execution</strong>: Terminal states (SUCCESS, FAILURE, CANCELLED) are cached to prevent re-execution</description></item>
/// <item><description><strong>Waiting Continuation</strong>: WAITING nodes can be re-processed via Resume/Fail operations</description></item>
/// <item><description><strong>Execution Counting</strong>: Tracks handler invocations across all clones for analytics</description></item>
/// </list>
/// 
/// <para><strong>State Management:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Status Caching</strong>: Terminal statuses are cached to ensure idempotency</description></item>
/// <item><description><strong>Conditional Processing</strong>: Evaluates conditions before each execution attempt</description></item>
/// <item><description><strong>Shared State</strong>: Execution count is shared among clones through base node reference</description></item>
/// </list>
/// 
/// <para><strong>Cloning Strategy:</strong></para>
/// <para>When cloned, new instances reference the original node as a base node to share execution count</para>
/// <para>while maintaining independent processing state. This allows global execution tracking while</para>
/// <para>supporting parallel processing scenarios.</para>
/// </remarks>
public class LSEventHandlerNode : ILSEventNode {
    /// <summary>
    /// Reference to the original node for sharing execution count across clones.
    /// Used to implement shared execution statistics while maintaining independent processing state.
    /// </summary>
    /// <remarks>
    /// When a node is cloned, the new instance references the original as its base node.
    /// This allows execution count to be shared globally while each clone maintains its own processing status.
    /// </remarks>
    protected LSEventHandlerNode? _baseNode;
    
    /// <summary>
    /// Local execution count for this specific node instance.
    /// Used when this node is not a clone (no base node reference).
    /// </summary>
    protected int _executionCount = 0;
    
    /// <summary>
    /// The handler delegate that contains the actual business logic to be executed.
    /// </summary>
    protected LSEventHandler _handler;
    
    /// <inheritdoc />
    public string NodeID { get; }
    
    /// <inheritdoc />
    public LSPriority Priority { get; }
    
    /// <inheritdoc />
    public LSEventCondition Conditions { get; }
    
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
    // Order is set during registration in the event system, starts at 0 and increases with each new node registered
    public int Order { get; }
    /// <summary>
    /// Initializes a new handler node with the specified configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for this node within its parent scope.</param>
    /// <param name="handler">The handler delegate to execute when this node is processed.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="baseNode">Base node reference for sharing execution count (used during cloning).</param>
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
    protected LSEventHandlerNode(string nodeID, LSEventHandler handler,
                      int order, LSPriority priority = LSPriority.NORMAL, LSEventHandlerNode? baseNode = null, params LSEventCondition?[] conditions) {
        _baseNode = baseNode;
        NodeID = nodeID;
        Priority = priority;
        _handler = handler;
        Order = order;
        var defaultCondition = (LSEventCondition)((ctx, node) => true);
        if (conditions == null || conditions.Length == 0) {
            Conditions = defaultCondition;
        } else {
            foreach (var condition in conditions) {
                if (condition != null) {
                    Conditions += condition;
                }
            }
        }
        if (Conditions == null) {
            Conditions = defaultCondition;
        }
    }

    /// <inheritdoc />
    public LSEventProcessStatus GetNodeStatus() {
        return _nodeStatus;
    }
    
    /// <summary>
    /// Current processing status of this handler node.
    /// Cached after first execution to ensure idempotency for terminal states.
    /// </summary>
    /// <remarks>
    /// This field is used to implement single-execution semantics for terminal states
    /// while allowing re-processing for WAITING states through Resume/Fail operations.
    /// </remarks>
    LSEventProcessStatus _nodeStatus = LSEventProcessStatus.UNKNOWN;

    /// <summary>
    /// Processes this handler node by executing its associated handler delegate.
    /// Implements single-execution semantics for terminal states while allowing re-processing for WAITING states.
    /// </summary>
    /// <param name="context">The processing context containing the event and system state.</param>
    /// <returns>The processing status after handler execution.</returns>
    /// <remarks>
    /// <para><strong>Execution Logic:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Terminal State Check</strong>: Returns cached status if already in SUCCESS, FAILURE, or CANCELLED state</description></item>
    /// <item><description><strong>Handler Invocation</strong>: Calls the associated handler delegate with event and node context</description></item>
    /// <item><description><strong>Execution Counting</strong>: Increments execution count regardless of handler outcome</description></item>
    /// <item><description><strong>Status Caching</strong>: Caches handler result for future calls, except when transitioning from WAITING</description></item>
    /// </list>
    /// 
    /// <para><strong>WAITING State Handling:</strong></para>
    /// <para>When the current status is WAITING, the handler is re-executed to allow for continuation</para>
    /// <para>logic. The status is only updated if it was previously UNKNOWN, preserving Resume/Fail transitions.</para>
    /// 
    /// <para><strong>Execution Count:</strong></para>
    /// <para>The execution count is incremented on every handler invocation, providing analytics</para>
    /// <para>on actual handler execution frequency regardless of caching or status outcomes.</para>
    /// </remarks>
    LSEventProcessStatus ILSEventNode.Process(LSEventProcessContext context) {
        //System.Console.WriteLine($"[LSEventHandlerNode] Processing node [{NodeID}], status: [{_nodeStatus}]");
        // node handler exit condition (because when WAITING we keep processing it)
        if (_nodeStatus != LSEventProcessStatus.UNKNOWN && _nodeStatus != LSEventProcessStatus.WAITING) {
            //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler already has final status {_nodeStatus}");
            return _nodeStatus; // already completed
        }

        // increment execution count indicating the handler was actually executed, doesn't matter the result, it always is recorded.
        // ExecutionCount uses _baseNode if available so the count is "shared" between clones
        // this may change in the future

        var nodeStatus = _handler(context.Event, context);
        ExecutionCount++;
        // we only update the node status if it was UNKNOWN, when _nodeStatus is not UNKNOWN it means Resume/Fail was already called
        _nodeStatus = (_nodeStatus == LSEventProcessStatus.UNKNOWN) ? nodeStatus : _nodeStatus;
        //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler executed with result [{nodeStatus}], _nodeStatus: {_nodeStatus}. Execution count is now {ExecutionCount}.");

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
    LSEventProcessStatus ILSEventNode.Resume(LSEventProcessContext context, params string[]? nodes) {
        //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler resume called, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");
        if (_nodeStatus != LSEventProcessStatus.WAITING && _nodeStatus != LSEventProcessStatus.UNKNOWN) {
            //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler cannot be resumed because its current status is [{_nodeStatus}].");
            return _nodeStatus; // can only resume if waiting or unknown
        }
        _nodeStatus = LSEventProcessStatus.SUCCESS;
        // if resume had no effect, return current status
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
    LSEventProcessStatus ILSEventNode.Fail(LSEventProcessContext context, params string[]? nodes) {
        //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler fail called, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");
        if (_nodeStatus != LSEventProcessStatus.WAITING && _nodeStatus != LSEventProcessStatus.UNKNOWN) {
            //System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler cannot be failed because its current status is [{_nodeStatus}].");
            return _nodeStatus; // can only fail if waiting or unknown
        }
        _nodeStatus = LSEventProcessStatus.FAILURE;
        // if resume had no effect, return current status
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
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        //System.Console.WriteLine($"[LSEventHandlerNode] Cancelling node {NodeID}.");
        _nodeStatus = LSEventProcessStatus.CANCELLED;
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
    public ILSEventNode Clone() {
        return new LSEventHandlerNode(NodeID, _handler, Order, Priority, this, Conditions);
    }

    /// <summary>
    /// Factory method for creating a new handler node with the specified configuration.
    /// Provides a convenient way to create handler nodes without exposing the protected constructor.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the handler node.</param>
    /// <param name="handler">Handler delegate containing the business logic to execute.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>New handler node instance ready for use in the event processing hierarchy.</returns>
    /// <remarks>
    /// This factory method creates original (non-clone) handler nodes by passing null as the baseNode parameter.
    /// The resulting node will track its own execution count rather than sharing with another node.
    /// </remarks>
    public static LSEventHandlerNode Create(string nodeID, LSEventHandler handler,
                      int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventHandlerNode(nodeID, handler, order, priority, null, conditions);
    }
}
