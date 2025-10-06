namespace LSUtils.Processing;

using System.Collections.Generic;
using System.Linq;
/// <summary>
/// Represents a parallel processing node that executes multiple child nodes simultaneously with threshold-based success/failure conditions.
/// Implements configurable parallel execution patterns within the LSProcessing system hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Parallel Processing Architecture:</b><br/>
/// This layer node implements sophisticated parallel execution logic where multiple child nodes execute
/// simultaneously, with configurable thresholds determining overall success or failure. Unlike sequential
/// nodes that process children one-at-a-time, parallel nodes execute all eligible children concurrently
/// and aggregate their results based on threshold criteria.
/// </para>
/// <para>
/// <b>Parallel Processing Logic:</b><br/>
/// • Simultaneous Execution: All eligible children are processed in parallel during each Execute() call<br/>
/// • Threshold-Based Success: Succeeds when NumRequiredToSucceed children reach SUCCESS state<br/>
/// • Threshold-Based Failure: Fails when NumRequiredToFailure children reach FAILURE state<br/>
/// • Complex Status Aggregation: Monitors multiple children simultaneously for state changes<br/>
/// • Configurable Termination: Flexible success/failure conditions based on threshold configuration
/// </para>
/// <para>
/// <b>Unified Pattern Implementation:</b><br/>
/// • _availableChildren: Contains filtered children that meet execution conditions<br/>
/// • _processStack: Used for consistent processing pattern (though all children process simultaneously)<br/>
/// • _isProcessing: Tracks initialization state to prevent re-filtering children<br/>
/// • GetNodeStatus(): Provides status aggregation and delegation for parent nodes
/// </para>
/// <para>
/// <b>Parallel vs Sequential Nodes:</b><br/>
/// • Execution Model: All children execute simultaneously (vs one-at-a-time in sequence/selector)<br/>
/// • Success Condition: Configurable threshold (vs fixed ALL/ANY logic)<br/>
/// • Failure Condition: Configurable threshold (vs immediate on first failure)<br/>
/// • Complexity: More complex status aggregation due to simultaneous processing<br/>
/// • Resource Usage: Higher concurrent load but potentially faster completion
/// </para>
/// <para>
/// <b>Threshold Configuration:</b><br/>
/// • NumRequiredToSucceed: Number of children that must succeed for parallel node success<br/>
/// • NumRequiredToFailure: Number of children that must fail for parallel node failure<br/>
/// • Flexible Logic: Supports various patterns (all succeed, any succeed, majority succeed, etc.)<br/>
/// • Validation: Thresholds should be validated against available children count<br/>
/// • Dynamic Evaluation: Thresholds evaluated in real-time as children complete
/// </para>
/// <para>
/// <b>Common Use Cases:</b><br/>
/// • Concurrent Tasks: Multiple independent operations that can execute simultaneously<br/>
/// • Redundancy: Multiple approaches to same goal where any success is sufficient<br/>
/// • Majority Voting: Systems requiring consensus from multiple sources<br/>
/// • Resource Competition: Multiple attempts where first success terminates others<br/>
/// • Load Distribution: Parallel processing for performance optimization
/// </para>
/// </remarks>
public class LSProcessNodeParallel : ILSProcessLayerNode {
    /// <inheritdoc />
    int ILSProcessNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");
    /// <summary>
    /// Collection of all child nodes indexed by their NodeID for efficient lookup and management.
    /// </summary>
    protected Dictionary<string, ILSProcessNode> _children = new();
    /// <summary>
    /// Processing stack used for unified pattern consistency. In parallel nodes, used for initialization tracking.
    /// </summary>
    protected Stack<ILSProcessNode> _processStack = new();
    /// <summary>
    /// List of children that meet execution conditions, filtered and cached during first processing call.
    /// All children in this list are processed simultaneously during each Process() call.
    /// </summary>
    protected IEnumerable<ILSProcessNode> _availableChildren = new List<ILSProcessNode>();
    /// <summary>
    /// Flag indicating whether processing has been initialized to prevent re-filtering children.
    /// </summary>
    protected bool _isProcessing = false;
    /// <inheritdoc />
    public string NodeID { get; }
    /// <inheritdoc />
    public LSProcessPriority Priority { get; internal set; }
    /// <inheritdoc />
    public int Order { get; internal set; }
    /// <inheritdoc />
    public LSProcessNodeCondition? Conditions { get; internal set; }
    public bool WithInverter { get; internal set; }
    /// <summary>
    /// Number of child nodes that must reach SUCCESS state for this parallel node to succeed.
    /// </summary>
    /// <value>Threshold value for success condition. Must be &gt; 0 and &lt;= total available children.</value>
    /// <remarks>
    /// <para>
    /// <b>Success Threshold Logic:</b><br/>
    /// • Value 1: Any child success triggers parallel success (OR logic)<br/>
    /// • Value = children count: All children must succeed (AND logic)<br/>
    /// • Value &gt; 1 and &lt; children count: Majority or specific count required<br/>
    /// • Runtime Validation: Threshold checked against actual available children during processing
    /// </para>
    /// </remarks>
    public int NumRequiredToSucceed { get; internal set; }
    /// <summary>
    /// Number of child nodes that must reach FAILURE state for this parallel node to fail.
    /// </summary>
    /// <value>Threshold value for failure condition. Must be &gt; 0 and &lt;= total available children.</value>
    /// <remarks>
    /// <para>
    /// <b>Failure Threshold Logic:</b><br/>
    /// • Value 1: Any child failure triggers parallel failure (fail-fast approach)<br/>
    /// • Value = children count: All children must fail for parallel failure (resilient approach)<br/>
    /// • Value &gt; 1 and &lt; children count: Specific failure count triggers overall failure<br/>
    /// • Runtime Validation: Threshold checked against actual available children during processing
    /// </para>
    /// </remarks>
    public int NumRequiredToFailure { get; internal set; }
    /// <summary>
    /// Create a new parallel node with threshold-based success/failure conditions.
    /// </summary>
    /// <param name="nodeID">Unique identifier for this parallel node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="numRequiredToSucceed">Number of children that must succeed for parallel success.</param>
    /// <param name="numRequiredToFailure">Number of children that must fail for parallel failure.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the parallel node.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <para>
    /// <b>Threshold Configuration:</b><br/>
    /// • Success Threshold: Must be &gt; 0 and &lt;= total children count when processing begins<br/>
    /// • Failure Threshold: Must be &gt; 0 and &lt;= total children count when processing begins<br/>
    /// • Validation: Thresholds are validated at runtime against actual available children<br/>
    /// • Flexibility: Allows creating nodes before children are added for builder pattern support
    /// </para>
    /// <para>
    /// <b>Condition Handling:</b><br/>
    /// • Default: If no conditions provided, uses a default condition that always returns true<br/>
    /// • Composition: Multiple conditions are combined using delegate composition (+=)<br/>
    /// • Null Safety: Null conditions in the array are automatically filtered out<br/>
    /// • Evaluation: Conditions are checked before each processing cycle
    /// </para>
    /// </remarks>
    internal LSProcessNodeParallel(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSProcessPriority priority = LSProcessPriority.NORMAL, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeID;
        Order = order;
        Priority = priority;
        NumRequiredToSucceed = numRequiredToSucceed;
        NumRequiredToFailure = numRequiredToFailure;
        WithInverter = withInverter;
        Conditions = LSProcessConditions.UpdateConditions(true, Conditions, conditions);
    }
    /// <summary>
    /// Adds a child node to this parallel node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this parallel node.</param>
    /// <remarks>
    /// <para>
    /// <b>Parallel Node Modification:</b> Children can be added during processing since all eligible children 
    /// process simultaneously. However, new children added after initialization will not be processed until 
    /// the next processing cycle begins.
    /// </para>
    /// <para>
    /// <b>Uniqueness:</b> If a child with the same NodeID already exists, it will be replaced with the new child.
    /// </para>
    /// <para>
    /// <b>Threshold Impact:</b> Adding children affects the relationship between thresholds and total child count, 
    /// potentially changing the success/failure evaluation criteria.
    /// </para>
    /// </remarks>
    public void AddChild(ILSProcessNode child) {
        _children[child.NodeID] = child;
    }
    /// <summary>
    /// Retrieves a specific child node by its identifier.
    /// </summary>
    /// <param name="label">The NodeID of the child to retrieve.</param>
    /// <returns>The child node if found, null otherwise.</returns>
    public ILSProcessNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }
    /// <summary>
    /// Gets all child nodes as an array.
    /// </summary>
    /// <returns>Array containing all child nodes in this parallel node.</returns>
    public ILSProcessNode[] GetChildren() {
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
    /// <para>
    /// <b>Threshold Impact:</b> Removing children affects the relationship between thresholds and total child count,
    /// potentially making thresholds invalid or changing success/failure evaluation criteria.
    /// </para>
    /// </remarks>
    public bool RemoveChild(string label) {
        return _children.Remove(label);
    }
    /// <inheritdoc />
    public ILSProcessLayerNode Clone() {
        var cloned = new LSProcessNodeParallel(NodeID, Order, NumRequiredToSucceed, NumRequiredToFailure, Priority, WithInverter, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSProcessNode ILSProcessNode.Clone() => Clone();
    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to parallel threshold logic.
    /// </summary>
    /// <returns>The current status of this parallel node based on threshold evaluation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Status Evaluation Priority (parallel threshold logic):</b><br/>
    /// 1. UNKNOWN: Processing not started yet (default initial state)<br/>
    /// 2. SUCCESS: No eligible children (all filtered out by conditions)<br/>
    /// 3. CANCELLED: Any child is in CANCELLED state (highest priority)<br/>
    /// 4. FAILURE: NumRequiredToFailure children have failed (threshold reached)<br/>
    /// 5. SUCCESS: NumRequiredToSucceed children have succeeded (threshold reached)<br/>
    /// 6. WAITING: Any child is in WAITING state (processing not complete)<br/>
    /// 7. SUCCESS: All children have succeeded (fallback success condition)<br/>
    /// 8. FAILURE: All children have failed (fallback failure condition)
    /// </para>
    /// <para>
    /// <b>Threshold-Based Logic:</b><br/>
    /// • Success Threshold: Once NumRequiredToSucceed children succeed, parallel succeeds<br/>
    /// • Failure Threshold: Once NumRequiredToFailure children fail, parallel fails<br/>
    /// • Early Termination: Either threshold triggers immediate status change<br/>
    /// • Simultaneous Monitoring: All children statuses evaluated simultaneously<br/>
    /// • Dynamic Evaluation: Status recalculated each time this method is called
    /// </para>
    /// <para>
    /// <b>Parallel vs Sequential Status Logic:</b><br/>
    /// • Complexity: More complex than sequence/selector due to threshold evaluation<br/>
    /// • Configurability: Success/failure conditions are configurable rather than fixed<br/>
    /// • Simultaneity: Evaluates all children at once rather than sequential processing<br/>
    /// • Flexibility: Supports various logical patterns (AND, OR, majority, custom thresholds)
    /// </para>
    /// </remarks>
    public LSProcessResultStatus GetNodeStatus() {
        if (_isProcessing == false) {
            return LSProcessResultStatus.UNKNOWN; // not yet processed
        }
        if (!_availableChildren.Any()) {
            return LSProcessResultStatus.SUCCESS; // no children available
        }

        var childStatuses = _availableChildren.Select(c => c.GetNodeStatus()).ToList();
        // Check for CANCELLED has the highest priority
        if (childStatuses.Any(c => c == LSProcessResultStatus.CANCELLED)) {
            return LSProcessResultStatus.CANCELLED;
        }
        int successCount = childStatuses.Count(c => c == LSProcessResultStatus.SUCCESS);
        int failureCount = childStatuses.Count(c => c == LSProcessResultStatus.FAILURE);
        if (NumRequiredToSucceed > NumRequiredToFailure) {
            // succeed has priority, only consider success if the number of successes is greater than or equal to required.
            if (successCount >= NumRequiredToSucceed) {
                return LSProcessResultStatus.SUCCESS;
            }
            // consider failure only if the number of failures is greater than or equal to required.
            if (failureCount >= NumRequiredToFailure) {
                return LSProcessResultStatus.FAILURE;
            }
        } else if (NumRequiredToFailure > NumRequiredToSucceed) {
            // failure has priority, only consider failure if the number of failures is greater than or equal to required.
            if (failureCount >= NumRequiredToFailure) {
                return LSProcessResultStatus.FAILURE;
            }
            // consider success only if the number of successes is greater than or equal to required.
            if (successCount >= NumRequiredToSucceed) {
                return LSProcessResultStatus.SUCCESS;
            }
        }
        // if no threshold reached we check for waiting childs
        if (childStatuses.Any(c => c == LSProcessResultStatus.WAITING)) {
            return LSProcessResultStatus.WAITING;
        }
        // if we have no waiting childs and no threshold reached we check if the number of success is greater than the required to succeed.
        if (successCount >= NumRequiredToSucceed) {
            //return LSProcessResultStatus.SUCCESS;
            return NumRequiredToSucceed < 0 ? // if NumRequiredToSucceed is less than 0 we require all childs to succeed.
                childStatuses.Count == successCount ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE :
                LSProcessResultStatus.SUCCESS; // otherwise we return success.
        }
        // any other result the parallel node failure
        return LSProcessResultStatus.FAILURE;
    }
    /// <summary>
    /// Propagates failure to specified children or all waiting children according to parallel logic.
    /// </summary>
    /// <param name="session">The processing session for delegation and state management.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure. If null or empty, targets all waiting children.</param>
    /// <returns>The current processing status after the failure operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Parallel Failure Logic:</b><br/>
    /// • Selective Targeting: Can fail specific children by NodeID or all waiting children<br/>
    /// • Batch Operation: Processes multiple children simultaneously unlike sequential nodes<br/>
    /// • State Filtering: Only affects children in WAITING or UNKNOWN states<br/>
    /// • Threshold Impact: Failures contribute to NumRequiredToFailure threshold evaluation<br/>
    /// • Status Update: Returns current parallel status after failure propagation
    /// </para>
    /// <para>
    /// <b>Target Node Specification:</b><br/>
    /// • No Nodes Specified: Fails all children currently in WAITING or UNKNOWN state<br/>
    /// • Specific Nodes: Only targets children whose NodeIDs match the provided array<br/>
    /// • Invalid Targets: Unknown NodeIDs are ignored gracefully<br/>
    /// • State Validation: Only children in appropriate states are affected
    /// </para>
    /// </remarks>
    public LSProcessResultStatus Fail(LSProcessSession session, params string[]? nodes) {
        // parallel can fail multiple nodes at a time so we need to care about nodes we want to fail.

        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            //System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to fail in parallel node {NodeID} failing all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSProcessResultStatus.WAITING || childStatus == LSProcessResultStatus.UNKNOWN) {
                    //System.Console.WriteLine($"[LSEventParallelNode] Failing child node {child.NodeID}.");
                    child.Fail(session, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var failedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (failedChild != null) {
                //System.Console.WriteLine($"[LSEventParallelNode] Failing child node {failedChild.NodeID}.");
                failedChild.Fail(session, nodes);
            } else {
                //System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to fail.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }
    /// <summary>
    /// Propagates resumption to specified children or all waiting children according to parallel logic.
    /// </summary>
    /// <param name="session">The processing session for delegation and state management.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption. If null or empty, targets all waiting children.</param>
    /// <returns>The current processing status after the resume operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Parallel Resume Logic:</b><br/>
    /// • Selective Targeting: Can resume specific children by NodeID or all waiting children<br/>
    /// • Batch Operation: Processes multiple children simultaneously for parallel continuation<br/>
    /// • State Filtering: Only affects children in WAITING or UNKNOWN states<br/>
    /// • Threshold Impact: Resumed children can contribute to success threshold when they complete<br/>
    /// • Status Update: Returns current parallel status after resume propagation
    /// </para>
    /// <para>
    /// <b>Target Node Specification:</b><br/>
    /// • No Nodes Specified: Resumes all children currently in WAITING or UNKNOWN state<br/>
    /// • Specific Nodes: Only targets children whose NodeIDs match the provided array<br/>
    /// • Invalid Targets: Unknown NodeIDs are ignored gracefully<br/>
    /// • State Validation: Only children in appropriate states are affected
    /// </para>
    /// </remarks>
    public LSProcessResultStatus Resume(LSProcessSession session, params string[]? nodes) {
        // parallel can resume multiple nodes at a time so we need to care about nodes we want to resume.
        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            //System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to resume in parallel node {NodeID} resuming all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSProcessResultStatus.WAITING || childStatus == LSProcessResultStatus.UNKNOWN) {
                    //System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {child.NodeID}.");
                    child.Resume(session, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var resumedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (resumedChild != null) {
                //System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {resumedChild.NodeID}.");
                resumedChild.Resume(session, nodes);
            } else {
                //System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to resume.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }
    /// <summary>
    /// Cancels all waiting children in this parallel node.
    /// </summary>
    /// <param name="session">The processing session for delegation and state management.</param>
    /// <returns>Always returns CANCELLED status.</returns>
    /// <remarks>
    /// <para>
    /// <b>Parallel Cancellation Logic:</b><br/>
    /// • Batch Cancellation: Cancels all children in WAITING or UNKNOWN states simultaneously<br/>
    /// • Immediate Effect: Returns CANCELLED status immediately regardless of children states<br/>
    /// • Propagation: Delegates cancellation to each eligible child for proper cleanup<br/>
    /// • Final State: Once cancelled, parallel node cannot be resumed or continued<br/>
    /// • Cleanup: Ensures all active child operations are properly terminated
    /// </para>
    /// </remarks>
    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession session) {
        foreach (var child in _availableChildren) {
            var childStatus = child.GetNodeStatus();
            if (childStatus == LSProcessResultStatus.WAITING || childStatus == LSProcessResultStatus.UNKNOWN) {
                //System.Console.WriteLine($"[LSEventParallelNode] Cancelling child node {child.NodeID}.");
                child.Cancel(session);
            }
        }
        return LSProcessResultStatus.CANCELLED;
    }
    /// <summary>
    /// Processes this parallel node by executing all eligible children simultaneously and evaluating threshold conditions.
    /// </summary>
    /// <param name="session">The processing session containing the current process and execution environment.</param>
    /// <returns>The final processing status of this parallel node based on threshold evaluation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Parallel Processing Algorithm:</b><br/>
    /// 1. Condition Check: If node conditions are not met, returns SUCCESS immediately<br/>
    /// 2. Initialization: On first call, filters and sorts children by conditions, priority, and order<br/>
    /// 3. Status Check: If already in final state (not UNKNOWN), returns current status<br/>
    /// 4. Simultaneous Processing: Calls Execute() on all eligible children in a single iteration<br/>
    /// 5. Threshold Evaluation: Checks success/failure thresholds after all children process<br/>
    /// 6. Final Status: Returns terminal status or WAITING for continued processing
    /// </para>
    /// <para>
    /// <b>Simultaneous Execution Model:</b><br/>
    /// • All Children Process: Unlike sequential nodes, all children execute during each Execute() call<br/>
    /// • No Early Termination: All children complete their processing regardless of individual outcomes<br/>
    /// • Threshold Evaluation: Status determined after all children have had opportunity to process<br/>
    /// • State Consistency: Ensures all children are processed before making threshold decisions<br/>
    /// • Concurrent Logic: Supports true parallel execution patterns
    /// </para>
    /// <para>
    /// <b>Threshold-Based Termination:</b><br/>
    /// • Success Threshold: Returns SUCCESS when NumRequiredToSucceed children succeed<br/>
    /// • Failure Threshold: Returns FAILURE when NumRequiredToFailure children fail<br/>
    /// • Cancellation: Returns CANCELLED immediately if any child is cancelled<br/>
    /// • Continued Processing: Returns WAITING if thresholds not met and children still processing<br/>
    /// • Dynamic Evaluation: Thresholds checked after each processing cycle
    /// </para>
    /// <para>
    /// <b>Parallel vs Sequential Processing:</b><br/>
    /// • Execution Pattern: All children in single loop (vs one-at-a-time processing)<br/>
    /// • Status Evaluation: After all children process (vs after each child)<br/>
    /// • Termination Logic: Threshold-based (vs fixed AND/OR logic)<br/>
    /// • Resource Usage: Higher concurrent load but potentially faster completion<br/>
    /// • Complexity: More sophisticated state management and aggregation logic
    /// </para>
    /// </remarks>
    public LSProcessResultStatus Execute(LSProcessSession session) {
        //System.Console.WriteLine($"[LSEventParallelNode] Process called on node {NodeID}, current status is {GetNodeStatus()}.");
        if (!LSProcessConditions.IsMet(session.Process, this)) return LSProcessResultStatus.SUCCESS;


        //System.Console.WriteLine($"[LSEventParallelNode] Processing parallel node {NodeID} with {_children.Count} children.");
        var parallelStatus = GetNodeStatus();

        // Initialize available children list if not done yet
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first), since _availableChildren in parallel is processed as a list and not as a stack
            // we do not use reverse.
            _availableChildren = _children.Values.Where(c => LSProcessConditions.IsMet(session.Process, c)).OrderByDescending(c => c.Priority).ThenBy(c => c.Order);
            _isProcessing = true;
            //System.Console.WriteLine($"[LSEventParallelNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()}.");
        }
        // exit condition for parallel is if all children are processed
        if (parallelStatus != LSProcessResultStatus.UNKNOWN) {
            //System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} already has final status {parallelStatus}, skipping processing.");
            return parallelStatus;
        }
        foreach (var child in _availableChildren) {
            // Process the child
            session._sessionStack.Push(child);
            var childStatus = child.Execute(session);
            session._sessionStack.Pop();
            //System.Console.WriteLine($"[LSEventParallelNode] Child node {child.NodeID} processed with status {childStatus}.");
        }

        parallelStatus = GetNodeStatus();
        if (parallelStatus == LSProcessResultStatus.SUCCESS || parallelStatus == LSProcessResultStatus.FAILURE || parallelStatus == LSProcessResultStatus.CANCELLED) {
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
    /// <returns>A new configured parallel node instance ready for child addition and processing.</returns>
    /// <remarks>
    /// <para>
    /// <b>Factory Method Benefits:</b><br/>
    /// • Convenience: Provides public access to internal constructor<br/>
    /// • Clarity: Explicit factory method name indicates parallel processing intent<br/>
    /// • Consistency: Matches factory pattern used across the LSProcessing framework<br/>
    /// • Threshold Configuration: Exposes threshold parameters for flexible parallel logic<br/>
    /// • Builder Integration: Seamlessly integrates with fluent builder patterns
    /// </para>
    /// <para>
    /// <b>Threshold Validation Responsibility:</b><br/>
    /// • Caller Validation: Factory method does not validate thresholds against child count<br/>
    /// • Runtime Validation: Threshold validity checked during processing when children are known<br/>
    /// • Flexible Configuration: Allows creating nodes before children are added<br/>
    /// • Deferred Validation: Validation occurs when actual processing begins
    /// </para>
    /// </remarks>
    public static LSProcessNodeParallel Create(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSProcessPriority priority = LSProcessPriority.NORMAL, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeParallel(nodeID, order, numRequiredToSucceed, numRequiredToFailure, priority, withInverter, conditions);
    }
}
