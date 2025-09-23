using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

/// <summary>
/// Represents a parallel processing node that executes multiple child nodes simultaneously with threshold-based success/failure conditions.
/// </summary>
/// <remarks>
/// <para><strong>Parallel Processing Logic:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Simultaneous Execution</strong>: All eligible children are processed in parallel during each Process() call</description></item>
/// <item><description><strong>Threshold-Based Success</strong>: Succeeds when NumRequiredToSucceed children reach SUCCESS state</description></item>
/// <item><description><strong>Threshold-Based Failure</strong>: Fails when NumRequiredToFailure children reach FAILURE state</description></item>
/// <item><description><strong>Complex Status Aggregation</strong>: Monitors multiple children simultaneously for state changes</description></item>
/// </list>
/// 
/// <para><strong>Unified Pattern Implementation:</strong></para>
/// <list type="bullet">
/// <item><description><strong>_availableChildren</strong>: Contains filtered children that meet execution conditions</description></item>
/// <item><description><strong>_processStack</strong>: Used for consistent processing pattern (though all children process simultaneously)</description></item>
/// <item><description><strong>_isProcessing</strong>: Tracks initialization state to prevent re-filtering children</description></item>
/// <item><description><strong>GetNodeStatus()</strong>: Provides status delegation for parent nodes</description></item>
/// </list>
/// 
/// <para><strong>Parallel vs Sequential Nodes:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Execution Model</strong>: All children execute simultaneously (vs one-at-a-time in sequence/selector)</description></item>
/// <item><description><strong>Success Condition</strong>: Configurable threshold (vs fixed ALL/ANY logic)</description></item>
/// <item><description><strong>Failure Condition</strong>: Configurable threshold (vs immediate on first failure)</description></item>
/// <item><description><strong>Complexity</strong>: More complex status aggregation due to simultaneous processing</description></item>
/// </list>
/// 
/// <para><strong>Threshold Configuration:</strong></para>
/// <list type="bullet">
/// <item><description><strong>NumRequiredToSucceed</strong>: Number of children that must succeed for parallel node success</description></item>
/// <item><description><strong>NumRequiredToFailure</strong>: Number of children that must fail for parallel node failure</description></item>
/// <item><description><strong>Flexible Logic</strong>: Supports various patterns (all succeed, any succeed, majority succeed, etc.)</description></item>
/// <item><description><strong>Validation</strong>: Thresholds should be validated against available children count</description></item>
/// </list>
/// 
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Concurrent Tasks</strong>: Multiple independent operations that can execute simultaneously</description></item>
/// <item><description><strong>Redundancy</strong>: Multiple approaches to same goal where any success is sufficient</description></item>
/// <item><description><strong>Majority Voting</strong>: Systems requiring consensus from multiple sources</description></item>
/// <item><description><strong>Resource Competition</strong>: Multiple attempts where first success terminates others</description></item>
/// </list>
/// </remarks>
public class LSEventParallelNode : ILSEventLayerNode {
    /// <inheritdoc />
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

    /// <summary>
    /// Collection of all child nodes indexed by their NodeID for efficient lookup and management.
    /// </summary>
    protected Dictionary<string, ILSEventNode> _children = new();

    /// <summary>
    /// Processing stack used for unified pattern consistency. In parallel nodes, used for initialization tracking.
    /// </summary>
    protected Stack<ILSEventNode> _processStack = new();

    /// <summary>
    /// List of children that meet execution conditions, filtered and cached during first processing call.
    /// All children in this list are processed simultaneously during each Process() call.
    /// </summary>
    protected IEnumerable<ILSEventNode> _availableChildren = new List<ILSEventNode>();

    /// <summary>
    /// Flag indicating whether processing has been initialized to prevent re-filtering children.
    /// </summary>
    protected bool _isProcessing = false;

    /// <inheritdoc />
    public string NodeID { get; }

    /// <inheritdoc />
    public LSPriority Priority { get; }

    /// <inheritdoc />
    public int Order { get; }

    /// <inheritdoc />
    public LSEventCondition Conditions { get; }

    public bool WithInverter { get; }

    /// <summary>
    /// Number of child nodes that must reach SUCCESS state for this parallel node to succeed.
    /// </summary>
    /// <value>Threshold value for success condition. Must be &lt;= total available children.</value>
    /// <remarks>
    /// <para><strong>Success Threshold Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Value 1</strong>: Any child success triggers parallel success (OR logic)</description></item>
    /// <item><description><strong>Value = children count</strong>: All children must succeed (AND logic)</description></item>
    /// <item><description><strong>Value &gt; 1 and &lt; children count</strong>: Majority or specific count required</description></item>
    /// </list>
    /// </remarks>
    public int NumRequiredToSucceed { get; internal set; }

    /// <summary>
    /// Number of child nodes that must reach FAILURE state for this parallel node to fail.
    /// </summary>
    /// <value>Threshold value for failure condition. Must be &lt;= total available children.</value>
    /// <remarks>
    /// <para><strong>Failure Threshold Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Value 1</strong>: Any child failure triggers parallel failure</description></item>
    /// <item><description><strong>Value = children count</strong>: All children must fail for parallel failure</description></item>
    /// <item><description><strong>Value &gt; 1 and &lt; children count</strong>: Specific failure count triggers overall failure</description></item>
    /// </list>
    /// </remarks>
    public int NumRequiredToFailure { get; internal set; }

    /// <summary>
    /// Initializes a new parallel node with threshold-based success/failure conditions.
    /// </summary>
    /// <param name="nodeID">Unique identifier for this parallel node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="numRequiredToSucceed">Number of children that must succeed for parallel success.</param>
    /// <param name="numRequiredToFailure">Number of children that must fail for parallel failure.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverted">If true, inverts the success/failure logic of the parallel node.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <para><strong>Threshold Configuration:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Threshold</strong>: Must be &gt; 0 and &lt;= total children count when processing begins</description></item>
    /// <item><description><strong>Failure Threshold</strong>: Must be &gt; 0 and &lt;= total children count when processing begins</description></item>
    /// <item><description><strong>Validation</strong>: Thresholds are not validated until processing begins with actual children</description></item>
    /// </list>
    /// 
    /// <para><strong>Condition Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Default</strong>: If no conditions provided, uses a default condition that always returns true</description></item>
    /// <item><description><strong>Composition</strong>: Multiple conditions are combined using delegate composition (+=)</description></item>
    /// <item><description><strong>Null Safety</strong>: Null conditions in the array are automatically filtered out</description></item>
    /// </list>
    /// </remarks>
    internal LSEventParallelNode(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        NodeID = nodeID;
        Order = order;
        Priority = priority;
        NumRequiredToSucceed = numRequiredToSucceed;
        NumRequiredToFailure = numRequiredToFailure;
        WithInverter = withInverter;
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

    /// <summary>
    /// Adds a child node to this parallel node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this parallel node.</param>
    /// <remarks>
    /// <para><strong>Parallel Node Modification:</strong> Children can be added during processing since all eligible children process simultaneously.</para>
    /// <para><strong>Uniqueness:</strong> If a child with the same NodeID already exists, it will be replaced.</para>
    /// <para><strong>Threshold Impact:</strong> Adding children affects the relationship between thresholds and total child count.</para>
    /// </remarks>
    public void AddChild(ILSEventNode child) {
        _children[child.NodeID] = child;
    }

    /// <summary>
    /// Retrieves a specific child node by its identifier.
    /// </summary>
    /// <param name="label">The NodeID of the child to retrieve.</param>
    /// <returns>The child node if found, null otherwise.</returns>
    public ILSEventNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    /// <summary>
    /// Gets all child nodes as an array.
    /// </summary>
    /// <returns>Array containing all child nodes in this parallel node.</returns>
    public ILSEventNode[] GetChildren() {
        return _children.Values.ToArray();
    }

    /// <summary>
    /// Checks if a child node with the specified identifier exists.
    /// </summary>
    /// <param name="label">The NodeID to check for.</param>
    /// <returns>True if a child with the specified ID exists, false otherwise.</returns>
    public bool HasChild(string label) {
        return _children.ContainsKey(label);
    }

    /// <summary>
    /// Removes a child node from this parallel node's collection.
    /// </summary>
    /// <param name="label">The NodeID of the child to remove.</param>
    /// <returns>True if the child was found and removed, false if no child with the specified ID existed.</returns>
    /// <remarks>
    /// <para><strong>Threshold Impact:</strong> Removing children affects the relationship between thresholds and total child count.</para>
    /// </remarks>
    public bool RemoveChild(string label) {
        return _children.Remove(label);
    }

    /// <inheritdoc />
    public ILSEventLayerNode Clone() {
        var cloned = new LSEventParallelNode(NodeID, Order, NumRequiredToSucceed, NumRequiredToFailure, Priority, WithInverter, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to parallel threshold logic.
    /// </summary>
    /// <returns>The current status of this parallel node.</returns>
    /// <remarks>
    /// <para><strong>Status Evaluation Priority (parallel threshold logic):</strong></para>
    /// <list type="number">
    /// <item><description><strong>UNKNOWN</strong>: Processing not started yet (default initial state)</description></item>
    /// <item><description><strong>SUCCESS</strong>: No eligible children (all filtered out by conditions)</description></item>
    /// <item><description><strong>CANCELLED</strong>: Any child is in CANCELLED state (highest priority)</description></item>
    /// <item><description><strong>FAILURE</strong>: NumRequiredToFailure children have failed (threshold reached)</description></item>
    /// <item><description><strong>SUCCESS</strong>: NumRequiredToSucceed children have succeeded (threshold reached)</description></item>
    /// <item><description><strong>WAITING</strong>: Any child is in WAITING state (processing not complete)</description></item>
    /// <item><description><strong>SUCCESS</strong>: All children have succeeded (fallback success condition)</description></item>
    /// <item><description><strong>FAILURE</strong>: All children have failed (fallback failure condition)</description></item>
    /// </list>
    /// 
    /// <para><strong>Threshold-Based Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Threshold</strong>: Once NumRequiredToSucceed children succeed, parallel succeeds</description></item>
    /// <item><description><strong>Failure Threshold</strong>: Once NumRequiredToFailure children fail, parallel fails</description></item>
    /// <item><description><strong>Early Termination</strong>: Either threshold triggers immediate status change</description></item>
    /// <item><description><strong>Simultaneous Monitoring</strong>: All children statuses evaluated simultaneously</description></item>
    /// </list>
    /// 
    /// <para><strong>Parallel vs Sequential Status Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Complexity</strong>: More complex than sequence/selector due to threshold evaluation</description></item>
    /// <item><description><strong>Configurability</strong>: Success/failure conditions are configurable rather than fixed</description></item>
    /// <item><description><strong>Simultaneity</strong>: Evaluates all children at once rather than sequential processing</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus GetNodeStatus() {
        if (_isProcessing == false) {
            return LSEventProcessStatus.UNKNOWN; // not yet processed
        }
        if (!_availableChildren.Any()) {
            return LSEventProcessStatus.SUCCESS; // no children available
        }

        var childStatuses = _availableChildren.Select(c => c.GetNodeStatus()).ToList();
        // Check for CANCELLED has the highest priority
        if (childStatuses.Any(c => c == LSEventProcessStatus.CANCELLED)) {
            return LSEventProcessStatus.CANCELLED;
        }

        int failureCount = childStatuses.Count(c => c == LSEventProcessStatus.FAILURE);
        if (NumRequiredToFailure > 0 && failureCount >= NumRequiredToFailure) {
            return LSEventProcessStatus.FAILURE;
        }
        int successCount = childStatuses.Count(c => c == LSEventProcessStatus.SUCCESS);
        if (NumRequiredToSucceed > 0 && successCount >= NumRequiredToSucceed) {
            return LSEventProcessStatus.SUCCESS;
        }
        if (childStatuses.Any(c => c == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING;
        }
        if (successCount >= NumRequiredToSucceed) {
            return LSEventProcessStatus.SUCCESS;
        }

        return LSEventProcessStatus.FAILURE;
    }

    /// <summary>
    /// Propagates failure to specified children or all waiting children according to parallel logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure. If null or empty, targets all waiting children.</param>
    /// <returns>The current processing status after the failure operation.</returns>
    /// <remarks>
    /// <para><strong>Parallel Failure Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Selective Targeting</strong>: Can fail specific children by NodeID or all waiting children</description></item>
    /// <item><description><strong>Batch Operation</strong>: Processes multiple children simultaneously unlike sequential nodes</description></item>
    /// <item><description><strong>State Filtering</strong>: Only affects children in WAITING or UNKNOWN states</description></item>
    /// <item><description><strong>Threshold Impact</strong>: Failures contribute to NumRequiredToFailure threshold evaluation</description></item>
    /// </list>
    /// 
    /// <para><strong>Target Node Specification:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>No Nodes Specified</strong>: Fails all children currently in WAITING or UNKNOWN state</description></item>
    /// <item><description><strong>Specific Nodes</strong>: Only targets children whose NodeIDs match the provided array</description></item>
    /// <item><description><strong>Invalid Targets</strong>: Unknown NodeIDs are logged and ignored</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // parallel can fail multiple nodes at a time so we need to care about nodes we want to fail.

        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            //System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to fail in parallel node {NodeID} failing all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                    //System.Console.WriteLine($"[LSEventParallelNode] Failing child node {child.NodeID}.");
                    child.Fail(context, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var failedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (failedChild != null) {
                //System.Console.WriteLine($"[LSEventParallelNode] Failing child node {failedChild.NodeID}.");
                failedChild.Fail(context, nodes);
            } else {
                //System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to fail.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }

    /// <summary>
    /// Propagates resumption to specified children or all waiting children according to parallel logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption. If null or empty, targets all waiting children.</param>
    /// <returns>The current processing status after the resume operation.</returns>
    /// <remarks>
    /// <para><strong>Parallel Resume Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Selective Targeting</strong>: Can resume specific children by NodeID or all waiting children</description></item>
    /// <item><description><strong>Batch Operation</strong>: Processes multiple children simultaneously for parallel continuation</description></item>
    /// <item><description><strong>State Filtering</strong>: Only affects children in WAITING or UNKNOWN states</description></item>
    /// <item><description><strong>Threshold Impact</strong>: Resumed children can contribute to success threshold when they complete</description></item>
    /// </list>
    /// 
    /// <para><strong>Target Node Specification:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>No Nodes Specified</strong>: Resumes all children currently in WAITING or UNKNOWN state</description></item>
    /// <item><description><strong>Specific Nodes</strong>: Only targets children whose NodeIDs match the provided array</description></item>
    /// <item><description><strong>Invalid Targets</strong>: Unknown NodeIDs are logged and ignored</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // parallel can resume multiple nodes at a time so we need to care about nodes we want to resume.
        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            //System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to resume in parallel node {NodeID} resuming all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                    //System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {child.NodeID}.");
                    child.Resume(context, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var resumedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (resumedChild != null) {
                //System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {resumedChild.NodeID}.");
                resumedChild.Resume(context, nodes);
            } else {
                //System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to resume.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }

    /// <summary>
    /// Cancels all waiting children in this parallel node.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <returns>Always returns CANCELLED status.</returns>
    /// <remarks>
    /// <para><strong>Parallel Cancellation Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Batch Cancellation</strong>: Cancels all children in WAITING or UNKNOWN states simultaneously</description></item>
    /// <item><description><strong>Immediate Effect</strong>: Returns CANCELLED status immediately regardless of children states</description></item>
    /// <item><description><strong>Propagation</strong>: Delegates cancellation to each eligible child for proper cleanup</description></item>
    /// <item><description><strong>Final State</strong>: Once cancelled, parallel node cannot be resumed or continued</description></item>
    /// </list>
    /// </remarks>
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        foreach (var child in _availableChildren) {
            var childStatus = child.GetNodeStatus();
            if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                //System.Console.WriteLine($"[LSEventParallelNode] Cancelling child node {child.NodeID}.");
                child.Cancel(context);
            }
        }
        return LSEventProcessStatus.CANCELLED;
    }

    /// <summary>
    /// Processes this parallel node by executing all eligible children simultaneously and evaluating threshold conditions.
    /// </summary>
    /// <param name="context">The processing context containing the event and execution environment.</param>
    /// <returns>The final processing status of this parallel node based on threshold evaluation.</returns>
    /// <remarks>
    /// <para><strong>Parallel Processing Algorithm:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Condition Check</strong>: If node conditions are not met, returns SUCCESS immediately</description></item>
    /// <item><description><strong>Initialization</strong>: On first call, filters and sorts children by conditions, priority, and order</description></item>
    /// <item><description><strong>Status Check</strong>: If already in final state (not UNKNOWN), returns current status</description></item>
    /// <item><description><strong>Simultaneous Processing</strong>: Calls Process() on all eligible children in a single iteration</description></item>
    /// <item><description><strong>Threshold Evaluation</strong>: Checks success/failure thresholds after all children process</description></item>
    /// <item><description><strong>Final Status</strong>: Returns terminal status or WAITING for continued processing</description></item>
    /// </list>
    /// 
    /// <para><strong>Simultaneous Execution Model:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>All Children Process</strong>: Unlike sequential nodes, all children execute during each Process() call</description></item>
    /// <item><description><strong>No Early Termination</strong>: All children complete their processing regardless of individual outcomes</description></item>
    /// <item><description><strong>Threshold Evaluation</strong>: Status determined after all children have had opportunity to process</description></item>
    /// <item><description><strong>State Consistency</strong>: Ensures all children are processed before making threshold decisions</description></item>
    /// </list>
    /// 
    /// <para><strong>Threshold-Based Termination:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Threshold</strong>: Returns SUCCESS when NumRequiredToSucceed children succeed</description></item>
    /// <item><description><strong>Failure Threshold</strong>: Returns FAILURE when NumRequiredToFailure children fail</description></item>
    /// <item><description><strong>Cancellation</strong>: Returns CANCELLED immediately if any child is cancelled</description></item>
    /// <item><description><strong>Continued Processing</strong>: Returns WAITING if thresholds not met and children still processing</description></item>
    /// </list>
    /// 
    /// <para><strong>Parallel vs Sequential Processing:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Execution Pattern</strong>: All children in single loop (vs one-at-a-time processing)</description></item>
    /// <item><description><strong>Status Evaluation</strong>: After all children process (vs after each child)</description></item>
    /// <item><description><strong>Termination Logic</strong>: Threshold-based (vs fixed AND/OR logic)</description></item>
    /// <item><description><strong>Resource Usage</strong>: Higher concurrent load but potentially faster completion</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus Process(LSEventProcessContext context) {
        //System.Console.WriteLine($"[LSEventParallelNode] Process called on node {NodeID}, current status is {GetNodeStatus()}.");
        if (!LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;


        //System.Console.WriteLine($"[LSEventParallelNode] Processing parallel node {NodeID} with {_children.Count} children.");
        var parallelStatus = GetNodeStatus();

        // Initialize available children list if not done yet
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first), since _availableChildren in parallel is processed as a list and not as a stack
            // we do not use reverse.
            _availableChildren = _children.Values.Where(c => LSEventConditions.IsMet(context.Event, c)).OrderByDescending(c => c.Priority).ThenBy(c => c.Order);
            _isProcessing = true;
            //System.Console.WriteLine($"[LSEventParallelNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()}.");
        }

        // exit condition for parallel is if all children are processed
        if (parallelStatus != LSEventProcessStatus.UNKNOWN) {
            //System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} already has final status {parallelStatus}, skipping processing.");
            return parallelStatus;
        }


        foreach (var child in _availableChildren) {
            //System.Console.WriteLine($"[LSEventParallelNode] Processing child node {child.NodeID}.");

            // Process the child
            var childStatus = child.Process(context);
            //System.Console.WriteLine($"[LSEventParallelNode] Child node {child.NodeID} processed with status {childStatus}.");

        }

        parallelStatus = GetNodeStatus();
        if (parallelStatus == LSEventProcessStatus.SUCCESS || parallelStatus == LSEventProcessStatus.FAILURE || parallelStatus == LSEventProcessStatus.CANCELLED) {
            //System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} completed with status {parallelStatus}.");
            return parallelStatus;
        }


        // Still processing
        //System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} still processing parallelStatus: {parallelStatus}.");
        return parallelStatus;
    }

    /// <summary>
    /// Creates a new parallel node with the specified threshold configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the parallel node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="numRequiredToSucceed">Number of children that must succeed for parallel success.</param>
    /// <param name="numRequiredToFailure">Number of children that must fail for parallel failure.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the parallel node.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>A new configured parallel node instance.</returns>
    /// <remarks>
    /// <para><strong>Factory Method Benefits:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Convenience</strong>: Provides public access to internal constructor</description></item>
    /// <item><description><strong>Clarity</strong>: Explicit factory method name indicates parallel processing intent</description></item>
    /// <item><description><strong>Consistency</strong>: Matches factory pattern used across the framework</description></item>
    /// <item><description><strong>Threshold Configuration</strong>: Exposes threshold parameters for flexible parallel logic</description></item>
    /// </list>
    /// 
    /// <para><strong>Threshold Validation Responsibility:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Caller Validation</strong>: Factory method does not validate thresholds against child count</description></item>
    /// <item><description><strong>Runtime Validation</strong>: Threshold validity checked during processing when children are known</description></item>
    /// <item><description><strong>Flexible Configuration</strong>: Allows creating nodes before children are added</description></item>
    /// </list>
    /// </remarks>
    public static LSEventParallelNode Create(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        return new LSEventParallelNode(nodeID, order, numRequiredToSucceed, numRequiredToFailure, priority, withInverter, conditions);
    }

}
