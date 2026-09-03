namespace LSUtils.ProcessSystem;

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
    public const string ClassName = nameof(LSProcessNodeSequence);
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
    public LSProcessPriority Priority { get; }
    /// <inheritdoc />
    public int Order { get; internal set; }
    /// <inheritdoc />
    public LSProcessNodeCondition?[] Conditions { get; }
    public bool ReadOnly => UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES);
    public NodeUpdatePolicy UpdatePolicy { get; }

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
    internal LSProcessNodeSequence(string nodeId, int order, LSProcessPriority priority = LSProcessPriority.NORMAL, NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.NONE, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
        UpdatePolicy = updatePolicy & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);
        Conditions = conditions;
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
            throw new LSException("Cannot add child after processing.");
        }
        _children[child.NodeID] = child;
    }
    public void AddChildren(params ILSProcessNode[] children) {
        foreach (var child in children) {
            AddChild(child);
        }
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
            throw new LSException("Cannot remove child after processing.");
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

        var cloned = new LSProcessNodeSequence(NodeID, Order, Priority, UpdatePolicy, Conditions);
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
        if (_isProcessing == false) return LSProcessResultStatus.UNKNOWN; // not yet processed
        var total = _availableChildren.Count();
        if (total == 0) return LSProcessResultStatus.SUCCESS; // no children to process, we are done
        int successCount = 0;
        foreach (var child in _availableChildren) {
            var status = child.GetNodeStatus();
            if (status == LSProcessResultStatus.CANCELLED) return LSProcessResultStatus.CANCELLED; // any cancelled child cancels the sequence
            if (status == LSProcessResultStatus.FAILURE) return LSProcessResultStatus.FAILURE; // any failed child fails the sequence
            if (status == LSProcessResultStatus.WAITING) return LSProcessResultStatus.WAITING; // any waiting child puts the sequence in waiting state
            if (status == LSProcessResultStatus.SUCCESS) successCount++;
        }
        return successCount == total ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.UNKNOWN;
    }
    /// <summary>
    /// Forces waiting children to transition to FAILURE state in sequence processing context.
    /// </summary>
    /// <param name="session">Processing context for the failure operation.</param>
    /// <returns>The sequence status after the failure operation.</returns>
    /// <remarks>
    /// <b>Sequence Behavior:</b> Since sequences process children one at a time, this method targets the first waiting child found rather than processing multiple nodes.<br/>
    /// <br/>
    /// <b>Delegation Pattern:</b> The failure operation is delegated to the waiting child, which then updates its status. The sequence status is recalculated based on child states.
    /// </remarks>
    public LSProcessResultStatus Fail(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Fail [{NodeID}]",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));
        if (!_isProcessing) {
            //log warning
            LSLogger.Singleton.Warning($"Cannot fail before processing.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("method", nameof(Fail))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
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
                    ("method", nameof(Fail))
                });
            return currentStatus; // nothing to fail
        }
        // NOTE: only a handler node actually fails, a layer node just propagates the fail, this is why when Fail a layer node can have any result
        var childStatus = _currentChild.Fail(session);
        if (childStatus == LSProcessResultStatus.FAILURE || childStatus == LSProcessResultStatus.CANCELLED) {
            endSequence();
            return childStatus;
        }

        if (childStatus == LSProcessResultStatus.WAITING) {
            return LSProcessResultStatus.WAITING; // still waiting
        }
        _currentChild = nextChild();
        // continue executing

        return Execute(session);

    }
    /// <inheritdoc />
    LSProcessResultStatus ILSProcessNode.Cancel(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug("LSProcessNodeSequence.Cancel",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID);

        // Cancel all children that are not already CANCELLED to enforce full process cancellation
        _availableChildren.Where(c => c.GetNodeStatus() != LSProcessResultStatus.CANCELLED)
            .ToList()
            .ForEach(c => c.Cancel(session));
        // update the node status
        var nodeStatus = GetNodeStatus();
        if (nodeStatus != LSProcessResultStatus.CANCELLED) {
            LSLogger.Singleton.Warning($"Sequence Node did not result CANCELLED.",
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
        return nodeStatus;
    }
    /// <summary>
    /// Resumes waiting children to continue sequence processing.
    /// </summary>
    /// <param name="session">Processing context for the resume operation.</param>
    /// <returns>The sequence status after the resume operation.</returns>
    /// <remarks>
    /// <b>Sequential Resume:</b> Finds the first waiting child and delegates the resume operation. Upon successful resume, the sequence may continue processing remaining children.<br/>
    /// <br/>
    /// <b>Status Propagation:</b> The resume result affects the overall sequence status based on whether the resumed child succeeds or encounters further issues.
    /// </remarks>
    public LSProcessResultStatus Resume(LSProcessSession session) {
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
                    ("method", nameof(Resume))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        var currentStatus = GetNodeStatus();
        if (_currentChild == null) return currentStatus;
        if (currentStatus != LSProcessResultStatus.WAITING && currentStatus != LSProcessResultStatus.UNKNOWN) {
            LSLogger.Singleton.Warning($"{ClassName} Node cannot continue resume.",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("currentChild", _currentChild.NodeID),
                    ("currentStatus", currentStatus),
                    ("availableChildren", _availableChildren.Count()),
                    ("method", nameof(Resume))
                });
            return currentStatus;
        }
        // NOTE: only a handler node actually resumes, a layer node just propagates the resume, this is why when Resume a layer node can have any result
        var childStatus = _currentChild.Resume(session);
        // NOTE: had to comment out the logging because was causing object reference not found, a proper logging should be done.
        // this is a gambiarra because I can't figure out the logic to do all operations and then log once, since I want log before exit the method
        // why this is a gambiarra? because the properties in the log should also have the updated _currentChild after nextChild() call
        // var dribleDaVaca = () => LSLogger.Singleton.Debug($"Node Sequence Resume.",
        //     source: (ClassName, null),
        //     processId: session.Process.ID,
        //     properties: new (string, object)[] {
        //                 ("nodeID", NodeID),
        //                 ("currentChild", _currentChild.NodeID),
        //                 ("availableChildren", _availableChildren.Count()),
        //                 ("method", nameof(Resume))
        //     });

        if (childStatus == LSProcessResultStatus.FAILURE || childStatus == LSProcessResultStatus.CANCELLED) {
            //failure and cancelled exit early
            //dribleDaVaca();
            endSequence();
            return childStatus;
        }
        if (childStatus == LSProcessResultStatus.WAITING) {
            //dribleDaVaca();
            return LSProcessResultStatus.WAITING; // still waiting
        }
        _currentChild = nextChild();
        // continue executing the session
        //dribleDaVaca();
        return Execute(session);
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
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Execute: [{NodeID}]. ",
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
            LSLogger.Singleton.Debug($"Execute Sequence node",
                  source: (ClassName, null),
                  processId: session.Process.ID,
                  properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("currentChild", _currentChild?.NodeID ?? "N/A"),
                    ("nodeChildren", _children.Count),
                    ("availableChildren", _availableChildren.Count()),
                    ("remainingChildren", _processStack.Count),
                    ("method", nameof(Execute))
                });
        }
        var sequenceStatus = GetNodeStatus();
        if (_currentChild == null) return LSProcessResultStatus.SUCCESS;

        do {
            session._sessionStack.Push(_currentChild);
            _currentChild.Execute(session);
            session._sessionStack.Pop();
            sequenceStatus = GetNodeStatus();
            // if sequenceStatus is still UNKNOWN the sequence is not done yet, so we can't just return UNKNOWN.
            // if (sequenceStatus == LSProcessResultStatus.UNKNOWN) { //THIS WAS ADDED BY MISTAKE, will be removed in the future, for now is commented out
            //     return LSProcessResultStatus.UNKNOWN;
            // }
            if (sequenceStatus == LSProcessResultStatus.FAILURE ||
                sequenceStatus == LSProcessResultStatus.CANCELLED) {
                endSequence();
                return sequenceStatus;
            }
            if (sequenceStatus == LSProcessResultStatus.WAITING) return LSProcessResultStatus.WAITING;
            _currentChild = nextChild();
        } while (_currentChild != null);

        return LSProcessResultStatus.SUCCESS;
    }
    void endSequence() {
        _processStack.Clear();
        _currentChild = null;
    }
    ILSProcessNode? nextChild() {
        // get the next node child
        return _processStack.Count > 0 ? _processStack.Pop() : null;
    }
    public void Reorder(int order) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Reorder [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        Order = order;
        int count = 0;
        foreach (var child in _children.Values) {
            child.Reorder(count++);
        }
    }
}
