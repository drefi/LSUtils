namespace LSUtils.Processing;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

/// <summary>
/// Layer node implementation that processes children sequentially with AND logic semantics.
/// Succeeds only when all children succeed, fails immediately when any child fails.
/// Serves as the reference implementation for the unified layer node processing pattern.
/// </summary>
/// <remarks>
/// <b>Processing Semantics (AND Logic):</b><br/>
/// • <b>Success Condition:</b> All children must succeed for the sequence to succeed<br/>
/// • <b>Failure Condition:</b> Any child failure immediately fails the entire sequence<br/>
/// • <b>Processing Order:</b> Children processed sequentially by Priority (descending) then Order (ascending)<br/>
/// • <b>Short-Circuit:</b> Processing stops immediately on first failure or cancellation<br/>
/// <br/>
/// <b>Unified Processing Pattern:</b><br/>
/// This class implements the standard pattern used across all layer nodes in LSProcessing system:<br/>
/// • <b>_availableChildren:</b> Condition-filtered and priority-sorted children list<br/>
/// • <b>_processStack:</b> Stack-based processing for deterministic execution order<br/>
/// • <b>_currentChild:</b> Current node being processed<br/>
/// • <b>_isProcessing:</b> One-time initialization flag preventing child modification during processing<br/>
/// • <b>GetNodeStatus():</b> Delegation pattern for status aggregation<br/>
/// <br/>
/// <b>State Management:</b><br/>
/// • <b>UNKNOWN:</b> Before processing starts (_isProcessing = false)<br/>
/// • <b>SUCCESS:</b> All children succeeded or no eligible children<br/>
/// • <b>FAILURE:</b> Any child failed<br/>
/// • <b>WAITING:</b> Any child is waiting for external completion<br/>
/// • <b>CANCELLED:</b> Any child was cancelled<br/>
/// <br/>
/// <b>Concurrency Considerations:</b><br/>
/// Once processing begins (_isProcessing = true), child modification is prevented to ensure processing integrity. Resume/Fail operations delegate to waiting children while maintaining the sequential processing contract.
/// </remarks>
public class LSProcessNodeSequence : ILSProcessLayerNode {
    /// <summary>
    /// Dictionary storing child nodes keyed by their NodeID for O(1) lookup operations.
    /// </summary>
    protected Dictionary<string, ILSProcessNode> _children = new();
    /// <summary>
    /// Current child node being processed. Null when no processing is active or all children are complete.
    /// </summary>
    protected ILSProcessNode? _currentChild;
    //protected LSProcessResultStatus _nodeSuccess => WithInverter ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS;
    //protected LSProcessResultStatus _nodeFailure => WithInverter ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
    /// <summary>
    /// Stack containing children to be processed, ordered by priority and execution order.
    /// Provides deterministic LIFO processing sequence.
    /// </summary>
    protected Stack<ILSProcessNode> _processStack = new();
    /// <summary>
    /// List of children eligible for processing after condition filtering and priority sorting.
    /// Populated during initialization and used for status aggregation.
    /// </summary>
    protected IEnumerable<ILSProcessNode> _availableChildren = new List<ILSProcessNode>();
    /// <summary>
    /// Flag indicating whether processing has been initialized.
    /// Once true, prevents child modification to ensure processing integrity.
    /// </summary>
    protected bool _isProcessing = false;
    /// <summary>
    /// Execution count is not tracked at the layer node level.
    /// Only handler nodes track execution statistics.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown as layer nodes don't track execution count.</exception>
    int ILSProcessNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");
    /// <inheritdoc />
    public string NodeID { get; }
    /// <inheritdoc />
    public LSProcessPriority Priority { get; internal set; }
    /// <inheritdoc />
    public int Order { get; internal set; }
    /// <inheritdoc />
    public LSProcessNodeCondition? Conditions { get; internal set; }
    //public bool WithInverter { get; internal set; }
    /// <summary>
    /// Initializes a new sequence node with the specified configuration.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this sequence node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the sequence.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <b>Condition Handling:</b><br/>
    /// • <b>Default:</b> If no conditions provided, uses a default condition that always returns true<br/>
    /// • <b>Composition:</b> Multiple conditions are combined using delegate composition (+=)<br/>
    /// • <b>Null Safety:</b> Null conditions in the array are automatically filtered out
    /// </remarks>
    protected LSProcessNodeSequence(string nodeId, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
        //WithInverter = withInverter;
        Conditions = LSProcessConditions.UpdateConditions(true, Conditions, conditions);
    }
    /// <summary>
    /// Adds a child node to this sequence node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this sequence.</param>
    /// <exception cref="InvalidOperationException">Thrown if called after processing has started.</exception>
    /// <remarks>
    /// <b>Processing Restriction:</b> Cannot add children once processing has begun (_isProcessing = true).<br/>
    /// <b>Uniqueness:</b> If a child with the same NodeID already exists, it will be replaced.
    /// </remarks>
    public void AddChild(ILSProcessNode child) {
        if (_isProcessing) {
            throw new System.InvalidOperationException("Cannot add child after processing.");
        }
        _children[child.NodeID] = child;
    }
    /// <summary>
    /// Removes a child node from this sequence node's collection.
    /// </summary>
    /// <param name="label">The NodeID of the child to remove.</param>
    /// <returns>True if the child was found and removed, false if no child with the specified ID existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if called after processing has started.</exception>
    /// <remarks>
    /// <b>Processing Restriction:</b> Cannot remove children once processing has begun (_isProcessing = true).
    /// </remarks>
    public bool RemoveChild(string label) {
        if (_isProcessing) {
            throw new System.InvalidOperationException("Cannot remove child after processing.");
        }
        return _children.Remove(label);
    }
    /// <inheritdoc />
    public ILSProcessNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }
    /// <inheritdoc />
    public bool HasChild(string label) => _children.ContainsKey(label);
    /// <inheritdoc />
    public ILSProcessNode[] GetChildren() => _children.Values.ToArray();
    /// <inheritdoc />
    public ILSProcessLayerNode Clone() {
        var cloned = new LSProcessNodeSequence(NodeID, Order, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSProcessNode ILSProcessNode.Clone() => Clone();
    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to sequence logic.
    /// </summary>
    /// <returns>The current status of this sequence node.</returns>
    /// <remarks>
    /// <b>Status Evaluation Priority (sequence AND logic):</b><br/>
    /// 1. <b>UNKNOWN:</b> Processing has not started yet (_isProcessing = false)<br/>
    /// 2. <b>SUCCESS:</b> No eligible children (all filtered out by conditions)<br/>
    /// 3. <b>CANCELLED:</b> Any child is in CANCELLED state (highest priority)<br/>
    /// 4. <b>WAITING:</b> Any child is in WAITING state (blocks further processing)<br/>
    /// 5. <b>FAILURE:</b> Any child is in FAILURE state (sequence fails immediately)<br/>
    /// 6. <b>SUCCESS:</b> All children are in SUCCESS state (sequence succeeds)<br/>
    /// <br/>
    /// <b>Edge Cases:</b><br/>
    /// • <b>No Children:</b> Returns SUCCESS when no eligible children exist<br/>
    /// • <b>Mixed States:</b> CANCELLED and WAITING take precedence over SUCCESS/FAILURE
    /// </remarks>
    public LSProcessResultStatus GetNodeStatus() {
        if (_isProcessing == false) {
            return LSProcessResultStatus.UNKNOWN; // not yet processed
        }
        if (!_availableChildren.Any()) {
            return LSProcessResultStatus.SUCCESS; // no children to process, we are done
        }

        var childStatuses = _availableChildren.Select(c => c.GetNodeStatus()).ToList();
        if (childStatuses.Any(c => c == LSProcessResultStatus.CANCELLED)) {
            return LSProcessResultStatus.CANCELLED;
        }
        if (childStatuses.Any(c => c == LSProcessResultStatus.FAILURE)) {
            return LSProcessResultStatus.FAILURE; // we found a failed child
        }
        if (childStatuses.Any(c => c == LSProcessResultStatus.WAITING)) {
            return LSProcessResultStatus.WAITING; // we have at least one child that is still waiting
        }
        return LSProcessResultStatus.SUCCESS; // all children are successful
    }
    /// <summary>
    /// Forces waiting children to transition to FAILURE state in sequence processing context.
    /// </summary>
    /// <param name="context">Processing context for the failure operation.</param>
    /// <param name="nodes">Optional node IDs to target (unused as sequence processes one child at a time).</param>
    /// <returns>The sequence status after the failure operation.</returns>
    /// <remarks>
    /// <b>Sequence Behavior:</b> Since sequences process children one at a time, this method targets the first waiting child found rather than processing multiple nodes.<br/>
    /// <br/>
    /// <b>Delegation Pattern:</b> The failure operation is delegated to the waiting child, which then updates its status. The sequence status is recalculated based on child states.
    /// </remarks>
    public LSProcessResultStatus Fail(LSProcessSession context, params string[]? nodes) {
        // sequence can only fail one node at a time so we don't need to care about nodes we just fail the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSProcessResultStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            //System.Console.WriteLine($"[LSEventSequenceNode] Failing waiting child node {waitingChild.NodeID}.");
            return waitingChild.Fail(context, nodes); //we propagate the fail to the waiting child
        }
        //System.Console.WriteLine($"[LSEventSequenceNode] No child node is in WAITING state, cannot fail sequence node {NodeID}.");


        return GetNodeStatus(); // we return the current sequence status.
    }
    /// <inheritdoc />
    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession context) {
        //return _currentChild?.Cancel(context) ?? LSProcessResultStatus.CANCELLED;
        return context.CurrentNode?.Cancel(context) ?? LSProcessResultStatus.CANCELLED;
    }
    /// <summary>
    /// Resumes waiting children to continue sequence processing.
    /// </summary>
    /// <param name="context">Processing context for the resume operation.</param>
    /// <param name="nodes">Optional node IDs to target (unused as sequence processes one child at a time).</param>
    /// <returns>The sequence status after the resume operation.</returns>
    /// <remarks>
    /// <b>Sequential Resume:</b> Finds the first waiting child and delegates the resume operation. Upon successful resume, the sequence may continue processing remaining children.<br/>
    /// <br/>
    /// <b>Status Propagation:</b> The resume result affects the overall sequence status based on whether the resumed child succeeds or encounters further issues.
    /// </remarks>
    public LSProcessResultStatus Resume(LSProcessSession context, params string[]? nodes) {
        // sequence can only resume one node at a time so we don't need to care about nodes we just resume the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSProcessResultStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            //System.Console.WriteLine($"[LSEventSequenceNode] Resuming waiting child node {waitingChild.NodeID}.");
            return waitingChild.Resume(context, nodes); //we propagate the resume to the waiting child
        }
        //System.Console.WriteLine($"[LSEventSequenceNode] No child node is in WAITING state, cannot resume sequence node {NodeID}.");
        return GetNodeStatus(); //we return the current sequence status, it may be SUCCESS if all children are done
    }
    /// <summary>
    /// Processes this sequence node and its children using the unified layer node processing pattern.
    /// Implements sequential AND logic where all children must succeed for the sequence to succeed.
    /// </summary>
    /// <param name="session">The processing context containing the event and system state.</param>
    /// <returns>The processing status after sequence processing.</returns>
    /// <remarks>
    /// <b>Processing Algorithm:</b><br/>
    /// 1. <b>Condition Check:</b> Return SUCCESS if node conditions are not met<br/>
    /// 2. <b>Initialization:</b> One-time setup of _availableChildren and _processStack<br/>
    /// 3. <b>Sequential Processing:</b> Process children in priority/order sequence<br/>
    /// 4. <b>Status Evaluation:</b> Check child results and determine continuation<br/>
    /// 5. <b>Completion:</b> Return final status when all children processed or failure occurs<br/>
    /// <br/>
    /// <b>Termination Conditions:</b><br/>
    /// • <b>WAITING:</b> Child is waiting, sequence blocks until Resume/Fail<br/>
    /// • <b>FAILURE:</b> Any child fails, sequence fails immediately (short-circuit)<br/>
    /// • <b>CANCELLED:</b> Any child cancelled, sequence terminates<br/>
    /// • <b>SUCCESS:</b> All children succeed, sequence succeeds<br/>
    /// <br/>
    /// <b>Child Ordering:</b><br/>
    /// Children are processed in Priority (descending) then Order (ascending) sequence. The stack is reversed to ensure correct processing order (lowest priority/order first).<br/>
    /// <br/>
    /// <b>State Persistence:</b><br/>
    /// Once _isProcessing is set to true, it never resets, ensuring processing consistency and preventing child modification during active processing.
    /// </remarks>
    public LSProcessResultStatus Execute(LSProcessSession session) {
        //System.Console.WriteLine($"[LSEventSequenceNode] Processing sequence node [{NodeID}]");
        //if (!LSProcessConditions.IsMet(session.Process, this)) return _nodeSuccess;

        // we should not need to check for CANCELLED. this is handled when calling the child.Process

        // create a stack to process children in order, this stack cannot be re-created after the node is processed.
        // so _isProcessing can never be set to false again after the first initialization
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first)
            _availableChildren = _children.Values.Where(c =>
                LSProcessConditions.IsMet(session.Process, c))
            .OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse().ToList();
            // Initialize the stack 
            _processStack = new Stack<ILSProcessNode>(_availableChildren);
            _isProcessing = true;
            if (_processStack.Count > 0) _currentChild = _processStack.Pop(); // set the current node to the first child
            //System.Console.WriteLine($"[LSEventSequenceNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()} {string.Join(", ", Array.ConvertAll(_availableChildren.ToArray(), c => c.NodeID))} _currentChild: {_currentChild?.NodeID}.");
        }

        var sequenceStatus = GetNodeStatus();
        // success condition: all children have been processed, no CurrentNode
        if (_currentChild == null) {
            // no children to process, we are done
            //System.Console.WriteLine($"[LSEventSequenceNode] No children to process for node {NodeID}, marking as SUCCESS.");
            // we keep processing the CurrentNode if available, otherwise we are done
            if (sequenceStatus != LSProcessResultStatus.SUCCESS) {
                // this should never be the case
                LSLogger.Singleton.Warning($"Sequence node [{NodeID}] has no children to process but status is <{sequenceStatus}>", $"{nameof(LSProcessNodeSequence)}.{nameof(Execute)}");
            }
            return LSProcessResultStatus.SUCCESS;
        }

        do {
            //System.Console.WriteLine($"[LSEventSequenceNode] Processing child node [{_currentChild.NodeID}].");

            // process the child
            session._sessionStack.Push(_currentChild);
            var currentChildStatus = _currentChild.Execute(session);
            session._sessionStack.Pop();
            sequenceStatus = GetNodeStatus();
            //System.Console.WriteLine($"[LSEventSequenceNode] Child node [{_currentChild.NodeID}] processed with status <{currentChildStatus}> sequenceStatus: <{sequenceStatus}>.");
            if (currentChildStatus == LSProcessResultStatus.WAITING) return LSProcessResultStatus.WAITING;
            if (sequenceStatus == LSProcessResultStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                //System.Console.WriteLine($"[LSEventSequenceNode] Warning: Sequence node [{NodeID}] is in WAITING state but child [{_currentChild.NodeID}] is not WAITING.");
                return LSProcessResultStatus.WAITING;
            }
            if (currentChildStatus == LSProcessResultStatus.FAILURE || sequenceStatus == LSProcessResultStatus.CANCELLED) {
                // exit condition in this node is clear the stack and current node
                //System.Console.WriteLine($"[LSEventSequenceNode] Ué Sequence node [{NodeID}] finished processing because child [{_currentChild.NodeID}] returned <{currentChildStatus}>.");
                _processStack.Clear();
                _currentChild = null;
                return sequenceStatus; // propagate the sequence status
            }

            // get the next node
            if (_processStack.Count == 0) {
                //System.Console.WriteLine($"[LSEventSequenceNode] Sequence node [{NodeID}] has processed all children.");
                _currentChild = null; // we are done
                break;
            }
            _currentChild = _processStack.Pop();
        } while (_currentChild != null);

        //reach this point means that the sequence was successfull
        //System.Console.WriteLine($"[LSEventSequenceNode] Sequence node [{NodeID}] finished processing all children. Final status: [{sequenceStatus}]");
        return LSProcessResultStatus.SUCCESS;
    }
    /// <summary>
    /// Factory method for creating a new sequence node with the specified configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the sequence node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the sequence.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>New sequence node instance ready for use in the event processing hierarchy.</returns>
    /// <remarks>
    /// <b>Factory Method Benefits:</b><br/>
    /// This factory method provides a convenient way to create sequence nodes without directly invoking the protected constructor. The created node implements AND logic semantics.
    /// </remarks>
    public static LSProcessNodeSequence Create(string nodeID, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeSequence(nodeID, order, priority, conditions);
    }
}
