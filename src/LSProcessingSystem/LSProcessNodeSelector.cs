using System.Collections.Generic;
using System.Linq;
namespace LSUtils.Processing;

/// <summary>
/// Layer node implementation that processes children sequentially with OR logic semantics.
/// Succeeds when any child succeeds, fails only when all children fail.
/// Follows the unified layer node processing pattern established by LSProcessNodeSequence.
/// </summary>
/// <remarks>
/// <b>Processing Semantics (OR Logic):</b><br/>
/// • <b>Success Condition:</b> Any child success immediately succeeds the entire selector<br/>
/// • <b>Failure Condition:</b> All children must fail for the selector to fail<br/>
/// • <b>Processing Order:</b> Children processed sequentially by Priority (descending) then Order (ascending)<br/>
/// • <b>Short-Circuit:</b> Processing stops immediately on first success or cancellation<br/>
/// <br/>
/// <b>Unified Processing Pattern:</b><br/>
/// This class implements the same pattern as LSProcessNodeSequence but with inverted success/failure logic:<br/>
/// • <b>_availableChildren:</b> Condition-filtered and priority-sorted children list<br/>
/// • <b>_processStack:</b> Stack-based processing for deterministic execution order<br/>
/// • <b>_currentChild:</b> Current node being processed<br/>
/// • <b>_isProcessing:</b> One-time initialization flag preventing child modification during processing<br/>
/// • <b>GetNodeStatus():</b> Delegation pattern for status aggregation<br/>
/// <br/>
/// <b>State Management:</b><br/>
/// • <b>UNKNOWN:</b> Before processing starts (_isProcessing = false)<br/>
/// • <b>SUCCESS:</b> Any child succeeded or no eligible children<br/>
/// • <b>FAILURE:</b> All children failed<br/>
/// • <b>WAITING:</b> Any child is waiting for external completion<br/>
/// • <b>CANCELLED:</b> Any child was cancelled<br/>
/// <br/>
/// <b>Selector vs Sequence Comparison:</b><br/>
/// • <b>Selector (OR):</b> Continues processing until any child succeeds or all fail<br/>
/// • <b>Sequence (AND):</b> Continues processing until any child fails or all succeed<br/>
/// • <b>Both:</b> Use identical processing infrastructure with different success/failure logic
/// </remarks>
public class LSProcessNodeSelector : ILSProcessLayerNode {
    /// <summary>
    /// Dictionary storing child nodes keyed by their NodeID for O(1) lookup operations.
    /// </summary>
    protected Dictionary<string, ILSProcessNode> _children = new();

    /// <summary>
    /// Current child node being processed. Null when no processing is active or processing is complete.
    /// </summary>
    protected ILSProcessNode? _currentChild;
    protected LSProcessResultStatus _nodeSuccess => WithInverter ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS;
    protected LSProcessResultStatus _nodeFailure => WithInverter ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;

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

    public bool WithInverter { get; internal set; }

    /// <summary>
    /// Initializes a new selector node with the specified configuration.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this selector node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the selector.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <b>Condition Handling:</b><br/>
    /// • <b>Default:</b> If no conditions provided, uses a default condition that always returns true<br/>
    /// • <b>Composition:</b> Multiple conditions are combined using delegate composition (+=)<br/>
    /// • <b>Null Safety:</b> Null conditions in the array are automatically filtered out
    /// </remarks>
    protected LSProcessNodeSelector(string nodeId, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
        WithInverter = withInverter;
        Conditions = LSProcessNodeExtensions.UpdateConditions(true, Conditions, conditions);
    }

    /// <summary>
    /// Adds a child node to this selector node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this selector.</param>
    /// <remarks>
    /// <b>No Processing Restriction:</b> Unlike sequence nodes, selectors allow child modification during processing.<br/>
    /// <b>Uniqueness:</b> If a child with the same NodeID already exists, it will be replaced.
    /// </remarks>
    public void AddChild(ILSProcessNode child) {
        _children[child.NodeID] = child;
    }

    /// <summary>
    /// Removes a child node from this selector node's collection.
    /// </summary>
    /// <param name="label">The NodeID of the child to remove.</param>
    /// <returns>True if the child was found and removed, false if no child with the specified ID existed.</returns>
    public bool RemoveChild(string label) {
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
        var cloned = new LSProcessNodeSelector(NodeID, Order, Priority, WithInverter, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSProcessNode ILSProcessNode.Clone() => Clone();

    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to selector logic.
    /// </summary>
    /// <returns>The current status of this selector node.</returns>
    /// <remarks>
    /// <b>Status Evaluation Priority (selector OR logic):</b><br/>
    /// 1. <b>SUCCESS:</b> No eligible children (all filtered out by conditions)<br/>
    /// 2. <b>CANCELLED:</b> Any child is in CANCELLED state (highest priority)<br/>
    /// 3. <b>SUCCESS:</b> Any child is in SUCCESS state (selector succeeds immediately)<br/>
    /// 4. <b>WAITING:</b> Any child is in WAITING state (blocks further processing)<br/>
    /// 5. <b>FAILURE:</b> All children are in FAILURE state (selector fails only when all fail)<br/>
    /// 6. <b>UNKNOWN:</b> Cannot determine status (default fallback)<br/>
    /// <br/>
    /// <b>OR Logic Differences from Sequence:</b><br/>
    /// • <b>Success Priority:</b> Any success immediately succeeds the selector<br/>
    /// • <b>Failure Requirement:</b> ALL children must fail for selector to fail<br/>
    /// • <b>Early Termination:</b> First success stops processing, unlike sequence which continues until failure
    /// </remarks>
    public LSProcessResultStatus GetNodeStatus() {
        if (_isProcessing == false) {
            return LSProcessResultStatus.UNKNOWN; // not yet processed
        }
        // Selector node status logic:
        // - If any child is in SUCCESS, the selector is in SUCCESS.
        // - If all children are in FAILURE, the selector is in FAILURE.
        // - If any child is in WAITING, the selector is in WAITING.
        // - If any child is in CANCELLED, the selector is in CANCELLED.
        // - if there are no children, the selector is in SUCCESS.
        if (_availableChildren.Count() == 0) return _nodeFailure;

        // check for CANCELLED has the highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSProcessResultStatus.CANCELLED)) {
            return LSProcessResultStatus.CANCELLED; // we have nothing to process anymore
        }
        // check for SUCCESS has the second highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSProcessResultStatus.SUCCESS)) {
            return _nodeSuccess; // we found a successful child
        }
        // check for WAITING has the third highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSProcessResultStatus.WAITING)) {
            return LSProcessResultStatus.WAITING; // we have at least one child that is still waiting
        }
        // check if all children have failed, if so the selector is FAILURE, otherwise cannot be determined
        return _availableChildren.Count() == _availableChildren.Count(c => c.GetNodeStatus() == LSProcessResultStatus.FAILURE) ? _nodeFailure : LSProcessResultStatus.UNKNOWN;
    }

    /// <summary>
    /// Propagates failure to waiting children according to selector logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure.</param>
    /// <returns>The current processing status after the failure operation.</returns>
    /// <remarks>
    /// <b>Selector Failure Logic:</b><br/>
    /// • <b>First Waiting Child:</b> Fails the first child found in WAITING state<br/>
    /// • <b>Propagation:</b> Delegates failure to the waiting child for proper handling<br/>
    /// • <b>OR Logic:</b> Selector fails only when all children have failed<br/>
    /// • <b>Status Return:</b> Returns current selector status after failure propagation<br/>
    /// <br/>
    /// If no children are waiting, returns the current selector status without modification.
    /// </remarks>
    public LSProcessResultStatus Fail(LSProcessSession context, params string[]? nodes) {
        // selector can only fail one node at a time so we don't need to care about nodes we just fail the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSProcessResultStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            //System.Console.WriteLine($"[LSEventSelectorNode] Failing waiting child node {waitingChild.NodeID}.");
            return waitingChild.Fail(context, nodes); //we propagate the fail to the waiting child
        }
        //System.Console.WriteLine($"[LSEventSelectorNode] No child node is in WAITING state, cannot fail selector node {NodeID}.");

        return GetNodeStatus(); // we return the current selector status.
    }

    /// <summary>
    /// Propagates resumption to waiting children according to selector logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption.</param>
    /// <returns>The current processing status after the resume operation.</returns>
    /// <remarks>
    /// <b>Selector Resume Logic:</b><br/>
    /// • <b>First Waiting Child:</b> Resumes the first child found in WAITING state<br/>
    /// • <b>Propagation:</b> Delegates resumption to the waiting child for continued processing<br/>
    /// • <b>OR Logic:</b> First child to succeed will make the entire selector succeed<br/>
    /// • <b>Status Return:</b> Returns current selector status after resume propagation<br/>
    /// <br/>
    /// If no children are waiting, returns the current selector status which may be SUCCESS if any child succeeded.
    /// </remarks>
    public LSProcessResultStatus Resume(LSProcessSession context, params string[]? nodes) {
        // selector can only resume one node at a time so we don't need to care about nodes we just resume the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSProcessResultStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            //System.Console.WriteLine($"[LSEventSelectorNode] Resuming waiting child node {waitingChild.NodeID}.");
            return waitingChild.Resume(context, nodes); //we propagate the resume to the waiting child
        }
        //System.Console.WriteLine($"[LSEventSelectorNode] No child node is in WAITING state, cannot resume selector node {NodeID}.");
        return GetNodeStatus(); //we return the current selector status, it may be SUCCESS if any child succeeded
    }

    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession context) {
        return _currentChild?.Cancel(context) ?? LSProcessResultStatus.CANCELLED;
    }

    /// <summary>
    /// Processes this selector node using sequential OR logic until the first child succeeds.
    /// </summary>
    /// <param name="context">The processing context containing the event and execution environment.</param>
    /// <returns>The final processing status of this selector node.</returns>
    /// <remarks>
    /// <b>Selector Processing Algorithm (OR Logic):</b><br/>
    /// 1. <b>Condition Check:</b> If node conditions are not met, returns SUCCESS immediately<br/>
    /// 2. <b>Initialization:</b> On first call, filters and sorts children by conditions, priority, and order<br/>
    /// 3. <b>Sequential Processing:</b> Processes children one by one in priority order<br/>
    /// 4. <b>Early Success:</b> Returns SUCCESS immediately when any child succeeds<br/>
    /// 5. <b>Continue on Failure:</b> Moves to next child when current child fails<br/>
    /// 6. <b>Final Failure:</b> Returns FAILURE only when all children have failed<br/>
    /// <br/>
    /// <b>State Management:</b><br/>
    /// • <b>_isProcessing:</b> Set to true after first initialization, never reset<br/>
    /// • <b>_processStack:</b> Contains children in reverse priority order for sequential processing<br/>
    /// • <b>_currentChild:</b> Points to the currently processing child node<br/>
    /// • <b>_availableChildren:</b> Filtered and sorted list of eligible children<br/>
    /// <br/>
    /// <b>OR Logic vs Sequence Logic:</b><br/>
    /// • <b>Success Condition:</b> ANY child success (vs ALL children success in sequence)<br/>
    /// • <b>Failure Condition:</b> ALL children fail (vs ANY child failure in sequence)<br/>
    /// • <b>Early Termination:</b> Stops on first success (vs stops on first failure in sequence)<br/>
    /// <br/>
    /// <b>Cancellation and Waiting:</b><br/>
    /// • <b>WAITING:</b> Returned immediately if current child is waiting<br/>
    /// • <b>CANCELLED:</b> Terminates processing and clears remaining stack<br/>
    /// • <b>State Preservation:</b> Processing state maintained across multiple calls
    /// </remarks>
    public LSProcessResultStatus Execute(LSProcessSession context) {
        //System.Console.WriteLine($"[LSEventSelectorNode] Processing selector node [{NodeID}] selectorStatus: <{selectorStatus}>");
        if (!LSProcessConditions.IsMet(context.Current, this)) return _nodeSuccess;


        // we should not need to check for CANCELLED. this is handled when calling the child.Process

        // create a stack to process children in order, this stack cannot be re-created after the node is processed.
        // so _isProcessing can never be set to false again after the first initialization
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first)
            _availableChildren = _children.Values.Where(c => LSProcessConditions.IsMet(context.Current, c)).OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse().ToList();
            // Initialize the stack 
            _processStack = new Stack<ILSProcessNode>(_availableChildren);
            _isProcessing = true;
            if (_processStack.Count > 0) _currentChild = _processStack.Pop(); // set the current node to the first child
            //System.Console.WriteLine($"[LSEventSelectorNode] Initialized processing for node [{NodeID}], children: {_availableChildren.Count()} _currentChild: {_currentChild?.NodeID}.");
        }
        // success condition: any child has succeeded or no children to process
        var selectorStatus = GetNodeStatus();
        if (_currentChild == null) {
            // no children to process, we are done
            //System.Console.WriteLine($"[LSEventSelectorNode] No children to process for node [{NodeID}], checking final status. selectorStatus {selectorStatus}");
            return selectorStatus; // return selector status
        }
        var successStatus = WithInverter ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS;
        var failureStatus = WithInverter ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
        do {
            //System.Console.WriteLine($"[LSEventSelectorNode] Processing child node [{_currentChild.NodeID}].");
            // no more need to check for condition, we already filtered children that meet conditions during stack initialization

            // process the child
            var currentChildStatus = _currentChild.Execute(context); //child status will only be used to update the selector state
            selectorStatus = GetNodeStatus();
            //System.Console.WriteLine($"[LSEventSelectorNode] Child node [{_currentChild.NodeID}] processed with status <{currentChildStatus}> selectorStatus: <{selectorStatus}>.");
            if (currentChildStatus == LSProcessResultStatus.WAITING) return LSProcessResultStatus.WAITING;
            if (selectorStatus == LSProcessResultStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                //System.Console.WriteLine($"[LSEventSelectorNode] Warning: Selector node [{NodeID}] is in WAITING state but child [{_currentChild.NodeID}] is not WAITING.");
                return LSProcessResultStatus.WAITING;
            }
            if (currentChildStatus == successStatus || selectorStatus == LSProcessResultStatus.CANCELLED) {
                // exit condition: child succeeded (selector success) or cancelled
                //System.Console.WriteLine($"[LSEventSelectorNode] Selector node [{NodeID}] finished processing because child [{_currentChild.NodeID}] returned {currentChildStatus}.");
                _processStack.Clear();
                _currentChild = null;
                return selectorStatus; // propagate the selector status
            }

            // get the next node if child failed (continue trying other children)
            if (_processStack.Count > 0) {
                _currentChild = _processStack.Pop();
            } else {
                _currentChild = null; // no more children
            }
        } while (_currentChild != null);

        // reach this point means that all children failed
        //System.Console.WriteLine($"[LSEventSelectorNode] Selector node [{NodeID}] finished processing all children. All failed, marking as FAILURE.");
        return failureStatus; // all children failed, selector fails
    }

    /// <summary>
    /// Creates a new selector node with the specified configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the selector node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the selector.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>A new configured selector node instance.</returns>
    /// <remarks>
    /// <b>Factory Method Benefits:</b><br/>
    /// • <b>Convenience:</b> Provides public access to protected constructor<br/>
    /// • <b>Clarity:</b> Explicit factory method name indicates intent<br/>
    /// • <b>Consistency:</b> Matches factory pattern used across the framework
    /// </remarks>
    public static LSProcessNodeSelector Create(string nodeID, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool withInverter = false, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeSelector(nodeID, order, priority, withInverter, conditions);
    }
}
