namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;

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
    public const string ClassName = nameof(LSProcessNodeSelector);
    /// <summary>
    /// Dictionary storing child nodes keyed by their NodeID for O(1) lookup operations.
    /// </summary>
    protected Dictionary<string, ILSProcessNode> _children = new();
    /// <summary>
    /// Current child node being processed. Null when no processing is active or processing is complete.
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
    public LSProcessLayerNodeType NodeType => LSProcessLayerNodeType.SELECTOR;
    /// <inheritdoc />
    public LSProcessPriority Priority { get; internal set; }
    /// <inheritdoc />
    public int Order { get; internal set; }
    /// <inheritdoc />
    public LSProcessNodeCondition? Conditions { get; internal set; }
    public bool ReadOnly { get; internal set; } = false;

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
    protected LSProcessNodeSelector(string nodeId, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
        ReadOnly = readOnly;
        Conditions = LSProcessHelpers.UpdateConditions(true, Conditions, conditions);
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
        if (_isProcessing) {
            throw new LSException("Cannot add child after processing.");
        }
        _children[child.NodeID] = child;
    }
    /// <summary>
    /// Removes a child node from this selector node's collection.
    /// </summary>
    /// <param name="label">The NodeID of the child to remove.</param>
    /// <returns>True if the child was found and removed, false if no child with the specified ID existed.</returns>
    public bool RemoveChild(string label) {
        if (_isProcessing) {
            throw new LSException("Cannot add child after processing.");
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
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Clone [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        var cloned = new LSProcessNodeSelector(NodeID, Order, Priority, ReadOnly, Conditions);
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
        if (!_isProcessing)
            return LSProcessResultStatus.UNKNOWN;
        int total = _availableChildren.Count();
        if (total == 0) return LSProcessResultStatus.FAILURE;
        int failed = 0;
        bool hasWaiting = false, hasSuccess = false;
        //NOTE: we can short-circuit on first CANCELLED, but for SUCCESS we still need to check all children since they can succeed later.
        foreach (var child in _availableChildren) {
            var status = child.GetNodeStatus();
            switch (status) {
                case LSProcessResultStatus.CANCELLED:
                    return LSProcessResultStatus.CANCELLED;
                case LSProcessResultStatus.SUCCESS:
                    hasSuccess = true;
                    break;
                case LSProcessResultStatus.WAITING:
                    hasWaiting = true;
                    break;
                case LSProcessResultStatus.FAILURE:
                    failed++;
                    break;
            }
        }
        if (hasWaiting) return LSProcessResultStatus.WAITING;
        if (hasSuccess) return LSProcessResultStatus.SUCCESS;
        if (failed == total)
            return LSProcessResultStatus.FAILURE;
        return LSProcessResultStatus.UNKNOWN;
    }
    /// <summary>
    /// Propagates failure to waiting children according to selector logic.
    /// </summary>
    /// <param name="session">The processing context for delegation and cancellation handling.</param>
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
    public LSProcessResultStatus Fail(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug("LSProcessNodeSelector.Fail",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID);

        if (!_isProcessing) throw new LSException("Cannot fail before processing.");
        if (_currentChild == null) return LSProcessResultStatus.SUCCESS;
        var currentStatus = GetNodeStatus();
        if (currentStatus != LSProcessResultStatus.WAITING && currentStatus != LSProcessResultStatus.UNKNOWN) {
            LSLogger.Singleton.Warning($"Node already in final state.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("currentChild", _currentChild?.NodeID ?? "null"),
                    ("currentStatus", currentStatus),
                    ("availableChildren", _availableChildren.Count()),
                    ("nodes", nodes == null ? "null" : string.Join(",", nodes)),
                    ("method", nameof(Fail))
                });
            return currentStatus; // nothing to fail
        }
        // NOTE: only a handler node actually fails, a layer node just propagates the fail, this is why when Fail a layer node can have any result
        var childStatus = _currentChild.Fail(session, nodes);
        if (childStatus == LSProcessResultStatus.SUCCESS || childStatus == LSProcessResultStatus.CANCELLED) {
            endSelector();
            return childStatus;
        }
        if (childStatus == LSProcessResultStatus.WAITING) {
            return LSProcessResultStatus.WAITING; // still waiting
        }
        _currentChild = nextChild();
        // continue executing the session

        return session.Execute();
    }
    /// <summary>
    /// Propagates resumption to waiting children according to selector logic.
    /// </summary>
    /// <param name="session">The processing context for delegation and cancellation handling.</param>
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
    public LSProcessResultStatus Resume(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Resume [{NodeID}]",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));

        if (!_isProcessing) {
            //log warning
            LSLogger.Singleton.Warning($"Cannot resume before processing.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                    ("method", nameof(Resume))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        var currentStatus = GetNodeStatus();
        if (_currentChild == null) return currentStatus;
        if (currentStatus != LSProcessResultStatus.WAITING && currentStatus != LSProcessResultStatus.UNKNOWN) {
            LSLogger.Singleton.Warning($"Selector Node cannot continue resume.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("currentChild", _currentChild.NodeID),
                    ("currentStatus", currentStatus),
                    ("availableChildren", _availableChildren.Count()),
                    ("nodes", nodes == null ? "null" : string.Join(",", nodes)),
                    ("method", nameof(Resume))
                });
            return currentStatus;
        }
        // NOTE: only a handler node actually resumes, a layer node just propagates the resume, this is why when Resume a layer node can have any result
        var childStatus = _currentChild.Resume(session, nodes);
        // this is a gambiarra because I can't figure out the logic to do all operations and then log once, since I want log before exit the method
        // why this is a gambiarra? because the properties in the log should also have the updated _currentChild after nextChild() call
        var dribleDaVaca = () => LSLogger.Singleton.Debug($"Node Selector Resume.",
            source: (ClassName, null),
            processId: session.Process.ID,
            properties: new (string, object)[] {
                        ("nodeID", NodeID),
                        ("currentChild", _currentChild.NodeID),
                        ("availableChildren", _availableChildren.Count()),
                        ("method", nameof(Resume))
            });

        if (childStatus == LSProcessResultStatus.SUCCESS || childStatus == LSProcessResultStatus.CANCELLED) {
            dribleDaVaca();
            endSelector();
            return childStatus;
        }
        if (childStatus == LSProcessResultStatus.WAITING) {
            dribleDaVaca();
            return LSProcessResultStatus.WAITING; // still waiting
        }
        _currentChild = nextChild();
        // continue executing the session
        dribleDaVaca();
        return session.Execute();
    }
    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug("LSProcessNodeSelector.Cancel",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID);

        if (!_isProcessing) throw new LSException("Cannot cancel before processing.");
        LSLogger.Singleton.Debug($"Selector Node Cancel.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("currentChild", _currentChild?.NodeID ?? "null"),
                ("availableChildren", _availableChildren.Count()),
                ("method", nameof(ILSProcessNode.Cancel))
            });
        // cancel waiting and unknown children
        _availableChildren.Where(c => {
            var cstatus = c.GetNodeStatus();
            return cstatus == LSProcessResultStatus.WAITING || cstatus == LSProcessResultStatus.UNKNOWN;
        }).ToList().ForEach(c => c.Cancel(session));
        // update the node status
        var nodeStatus = GetNodeStatus();
        if (nodeStatus != LSProcessResultStatus.CANCELLED) {
            LSLogger.Singleton.Warning($"Selector Node did not result CANCELLED.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodeStatus", nodeStatus),
                    ("currentChild", _currentChild?.NodeID ?? "null"),
                    ("availableChildren", _availableChildren.Count()),
                    ("method", nameof(ILSProcessNode.Cancel))
                });
        }
        // we return cancelled because it should always be cancelled
        return LSProcessResultStatus.CANCELLED;
    }
    /// <summary>
    /// Processes this selector node using sequential OR logic until the first child succeeds.
    /// </summary>
    /// <param name="session">The processing context containing the event and execution environment.</param>
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
    public LSProcessResultStatus Execute(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Execute [{NodeID}]. ",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));

        if (_isProcessing == false) {
            // will only process children that meet conditions, children ordered by Priority (critical first) and Order (lowest first)
            _availableChildren = _children.Values
                .Where(c => LSProcessHelpers.IsMet(session.Process, c))
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.Order).Reverse().ToList();
            // Initialize the stack 
            _processStack = new Stack<ILSProcessNode>(_availableChildren);
            _isProcessing = true;
            // get the first child to process
            _currentChild = nextChild();
            LSLogger.Singleton.Debug($"Executing Selector Node.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("currentChild", _currentChild?.NodeID ?? "null"),
                    ("childrens", _children.Count),
                    ("remainingChildren", _processStack.Count),
                    ("method", nameof(Execute))
                });
        }
        var selectorStatus = GetNodeStatus();
        if (_currentChild == null) return selectorStatus;

        do {
            session._sessionStack.Push(_currentChild);
            _currentChild.Execute(session);
            session._sessionStack.Pop();
            selectorStatus = GetNodeStatus();
            if (selectorStatus == LSProcessResultStatus.CANCELLED ||
                selectorStatus == LSProcessResultStatus.SUCCESS) {
                endSelector();
                return selectorStatus;
            }
            if (selectorStatus == LSProcessResultStatus.WAITING) {
                return LSProcessResultStatus.WAITING;
            }
            _currentChild = nextChild();
        } while (_currentChild != null);
        // if all children failed selector also fails
        return LSProcessResultStatus.FAILURE;
    }
    void endSelector() {
        // we clear the stack and current child to acknowledge processing is finished
        _processStack.Clear();
        _currentChild = null;

    }
    ILSProcessNode? nextChild() {
        // get the next node child
        return _processStack.Count > 0 ? _processStack.Pop() : null;
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
    public static LSProcessNodeSelector Create(string nodeID, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        return new LSProcessNodeSelector(nodeID, order, priority, readOnly, conditions);
    }
}
