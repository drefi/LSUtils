using System;
using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

/// <summary>
/// Layer node implementation that processes children sequentially with AND logic semantics.
/// Succeeds only when all children succeed, fails immediately when any child fails.
/// Serves as the reference implementation for the unified layer node processing pattern.
/// </summary>
/// <remarks>
/// <para><strong>Processing Semantics (AND Logic):</strong></para>
/// <list type="bullet">
/// <item><description><strong>Success Condition</strong>: All children must succeed for the sequence to succeed</description></item>
/// <item><description><strong>Failure Condition</strong>: Any child failure immediately fails the entire sequence</description></item>
/// <item><description><strong>Processing Order</strong>: Children processed sequentially by Priority (descending) then Order (ascending)</description></item>
/// <item><description><strong>Short-Circuit</strong>: Processing stops immediately on first failure or cancellation</description></item>
/// </list>
/// 
/// <para><strong>Unified Processing Pattern:</strong></para>
/// <para>This class implements the standard pattern used across all layer nodes in LSEventSystem v5:</para>
/// <list type="bullet">
/// <item><description><strong>_availableChildren</strong>: Condition-filtered and priority-sorted children list</description></item>
/// <item><description><strong>_processStack</strong>: Stack-based processing for deterministic execution order</description></item>
/// <item><description><strong>_currentChild</strong>: Current node being processed</description></item>
/// <item><description><strong>_isProcessing</strong>: One-time initialization flag preventing child modification during processing</description></item>
/// <item><description><strong>GetNodeStatus()</strong>: Delegation pattern for status aggregation</description></item>
/// </list>
/// 
/// <para><strong>State Management:</strong></para>
/// <list type="bullet">
/// <item><description><strong>UNKNOWN</strong>: Before processing starts (_isProcessing = false)</description></item>
/// <item><description><strong>SUCCESS</strong>: All children succeeded or no eligible children</description></item>
/// <item><description><strong>FAILURE</strong>: Any child failed</description></item>
/// <item><description><strong>WAITING</strong>: Any child is waiting for external completion</description></item>
/// <item><description><strong>CANCELLED</strong>: Any child was cancelled</description></item>
/// </list>
/// 
/// <para><strong>Concurrency Considerations:</strong></para>
/// <para>Once processing begins (_isProcessing = true), child modification is prevented to ensure</para>
/// <para>processing integrity. Resume/Fail operations delegate to waiting children while maintaining</para>
/// <para>the sequential processing contract.</para>
/// </remarks>
/// <summary>
/// An event node that processes its children in order until one fails.
/// </summary>
public class LSEventSequenceNode : ILSEventLayerNode {
    /// <summary>
    /// Dictionary storing child nodes keyed by their NodeID for O(1) lookup operations.
    /// </summary>
    protected Dictionary<string, ILSEventNode> _children = new();

    /// <summary>
    /// Current child node being processed. Null when no processing is active or all children are complete.
    /// </summary>
    ILSEventNode? _currentChild;

    /// <summary>
    /// Stack containing children to be processed, ordered by priority and execution order.
    /// Provides deterministic LIFO processing sequence.
    /// </summary>
    protected Stack<ILSEventNode> _processStack = new();

    /// <summary>
    /// List of children eligible for processing after condition filtering and priority sorting.
    /// Populated during initialization and used for status aggregation.
    /// </summary>
    //protected Dictionary<ILSEventNode, LSEventProcessStatus> _childrenStatuses = new();
    List<ILSEventNode> _availableChildren = new();

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
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

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
    /// Initializes a new sequence node with the specified configuration.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this sequence node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="withInverter">If true, inverts the success/failure logic of the sequence.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <para><strong>Condition Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Default</strong>: If no conditions provided, uses a default condition that always returns true</description></item>
    /// <item><description><strong>Composition</strong>: Multiple conditions are combined using delegate composition (+=)</description></item>
    /// <item><description><strong>Null Safety</strong>: Null conditions in the array are automatically filtered out</description></item>
    /// </list>
    /// </remarks>
    protected LSEventSequenceNode(string nodeId, int order, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
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
    /// Adds a child node to this sequence node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this sequence.</param>
    /// <exception cref="InvalidOperationException">Thrown if called after processing has started.</exception>
    /// <remarks>
    /// <para><strong>Processing Restriction:</strong> Cannot add children once processing has begun (_isProcessing = true).</para>
    /// <para><strong>Uniqueness:</strong> If a child with the same NodeID already exists, it will be replaced.</para>
    /// </remarks>
    public void AddChild(ILSEventNode child) {
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
    /// <para><strong>Processing Restriction:</strong> Cannot remove children once processing has begun (_isProcessing = true).</para>
    /// </remarks>
    public bool RemoveChild(string label) {
        if (_isProcessing) {
            throw new System.InvalidOperationException("Cannot remove child after processing.");
        }
        return _children.Remove(label);
    }

    /// <inheritdoc />
    public ILSEventNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    /// <inheritdoc />
    public bool HasChild(string label) => _children.ContainsKey(label);

    /// <inheritdoc />
    public ILSEventNode[] GetChildren() => _children.Values.ToArray();

    /// <inheritdoc />
    public ILSEventLayerNode Clone() {
        var cloned = new LSEventSequenceNode(NodeID, Order, Priority, WithInverter, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to sequence logic.
    /// </summary>
    /// <returns>The current status of this sequence node.</returns>
    /// <remarks>
    /// <para><strong>Status Evaluation Priority (sequence AND logic):</strong></para>
    /// <list type="number">
    /// <item><description><strong>UNKNOWN</strong>: Processing has not started yet (_isProcessing = false)</description></item>
    /// <item><description><strong>SUCCESS</strong>: No eligible children (all filtered out by conditions)</description></item>
    /// <item><description><strong>CANCELLED</strong>: Any child is in CANCELLED state (highest priority)</description></item>
    /// <item><description><strong>WAITING</strong>: Any child is in WAITING state (blocks further processing)</description></item>
    /// <item><description><strong>FAILURE</strong>: Any child is in FAILURE state (sequence fails immediately)</description></item>
    /// <item><description><strong>SUCCESS</strong>: All children are in SUCCESS state (sequence succeeds)</description></item>
    /// </list>
    /// 
    /// <para><strong>Edge Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>No Children</strong>: Returns SUCCESS when no eligible children exist</description></item>
    /// <item><description><strong>Mixed States</strong>: CANCELLED and WAITING take precedence over SUCCESS/FAILURE</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus GetNodeStatus() {
        if (_isProcessing == false) {
            return LSEventProcessStatus.UNKNOWN; // not yet processed
        }
        if (!_availableChildren.Any()) {
            return !WithInverter ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE; // no children available
        }

        var childStatuses = _availableChildren.Select(c => c.GetNodeStatus()).ToList();
        // Check for CANCELLED has the highest priority
        if (childStatuses.Any(c => c == LSEventProcessStatus.CANCELLED)) {
            return LSEventProcessStatus.CANCELLED;
        }
        if (childStatuses.Any(c => c == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING; // we have at least one child that is still waiting
        }
        // check for FAILURE has the third highest priority
        if (childStatuses.Any(c => c == LSEventProcessStatus.FAILURE)) {
            return !WithInverter ? LSEventProcessStatus.FAILURE : LSEventProcessStatus.SUCCESS; // we do not need to continue processing
        }
        return !WithInverter ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE; // all children are successful
    }
    /// <summary>
    /// Forces waiting children to transition to FAILURE state in sequence processing context.
    /// </summary>
    /// <param name="context">Processing context for the failure operation.</param>
    /// <param name="nodes">Optional node IDs to target (unused as sequence processes one child at a time).</param>
    /// <returns>The sequence status after the failure operation.</returns>
    /// <remarks>
    /// <para><strong>Sequence Behavior:</strong> Since sequences process children one at a time,</para>
    /// <para>this method targets the first waiting child found rather than processing multiple nodes.</para>
    /// 
    /// <para><strong>Delegation Pattern:</strong> The failure operation is delegated to the waiting child,</para>
    /// <para>which then updates its status. The sequence status is recalculated based on child states.</para>
    /// </remarks>
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // sequence can only fail one node at a time so we don't need to care about nodes we just fail the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            //System.Console.WriteLine($"[LSEventSequenceNode] Failing waiting child node {waitingChild.NodeID}.");
            return waitingChild.Fail(context, nodes); //we propagate the fail to the waiting child
        }
        //System.Console.WriteLine($"[LSEventSequenceNode] No child node is in WAITING state, cannot fail sequence node {NodeID}.");


        return GetNodeStatus(); // we return the current sequence status.
    }

    /// <inheritdoc />
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        return _currentChild?.Cancel(context) ?? LSEventProcessStatus.CANCELLED;
    }


    /// <summary>
    /// Resumes waiting children to continue sequence processing.
    /// </summary>
    /// <param name="context">Processing context for the resume operation.</param>
    /// <param name="nodes">Optional node IDs to target (unused as sequence processes one child at a time).</param>
    /// <returns>The sequence status after the resume operation.</returns>
    /// <remarks>
    /// <para><strong>Sequential Resume:</strong> Finds the first waiting child and delegates the resume operation.</para>
    /// <para>Upon successful resume, the sequence may continue processing remaining children.</para>
    /// 
    /// <para><strong>Status Propagation:</strong> The resume result affects the overall sequence status</para>
    /// <para>based on whether the resumed child succeeds or encounters further issues.</para>
    /// </remarks>
    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // sequence can only resume one node at a time so we don't need to care about nodes we just resume the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
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
    /// <param name="context">The processing context containing the event and system state.</param>
    /// <returns>The processing status after sequence processing.</returns>
    /// <remarks>
    /// <para><strong>Processing Algorithm:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Condition Check</strong>: Return SUCCESS if node conditions are not met</description></item>
    /// <item><description><strong>Initialization</strong>: One-time setup of _availableChildren and _processStack</description></item>
    /// <item><description><strong>Sequential Processing</strong>: Process children in priority/order sequence</description></item>
    /// <item><description><strong>Status Evaluation</strong>: Check child results and determine continuation</description></item>
    /// <item><description><strong>Completion</strong>: Return final status when all children processed or failure occurs</description></item>
    /// </list>
    /// 
    /// <para><strong>Termination Conditions:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>WAITING</strong>: Child is waiting, sequence blocks until Resume/Fail</description></item>
    /// <item><description><strong>FAILURE</strong>: Any child fails, sequence fails immediately (short-circuit)</description></item>
    /// <item><description><strong>CANCELLED</strong>: Any child cancelled, sequence terminates</description></item>
    /// <item><description><strong>SUCCESS</strong>: All children succeed, sequence succeeds</description></item>
    /// </list>
    /// 
    /// <para><strong>Child Ordering:</strong></para>
    /// <para>Children are processed in Priority (descending) then Order (ascending) sequence.</para>
    /// <para>The stack is reversed to ensure correct processing order (lowest priority/order first).</para>
    /// 
    /// <para><strong>State Persistence:</strong></para>
    /// <para>Once _isProcessing is set to true, it never resets, ensuring processing consistency</para>
    /// <para>and preventing child modification during active processing.</para>
    /// </remarks>
    public LSEventProcessStatus Process(LSEventProcessContext context) {
        //System.Console.WriteLine($"[LSEventSequenceNode] Processing sequence node [{NodeID}]");
        if (!LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;

        var sequenceStatus = GetNodeStatus();

        // we should not need to check for CANCELLED. this is handled when calling the child.Process

        // create a stack to process children in order, this stack cannot be re-created after the node is processed.
        // so _isProcessing can never be set to false again after the first initialization
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first)
            _availableChildren = _children.Values.Where(c => LSEventConditions.IsMet(context.Event, c)).OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse().ToList();
            // Initialize the stack 
            _processStack = new Stack<ILSEventNode>(_availableChildren);
            _isProcessing = true;
            if (_processStack.Count > 0) _currentChild = _processStack.Pop(); // set the current node to the first child
            //System.Console.WriteLine($"[LSEventSequenceNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()} {string.Join(", ", Array.ConvertAll(_availableChildren.ToArray(), c => c.NodeID))} _currentChild: {_currentChild?.NodeID}.");
        }
        var successStatus = WithInverter ? LSEventProcessStatus.FAILURE : LSEventProcessStatus.SUCCESS;
        var failureStatus = WithInverter ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE;

        // success condition: all children have been processed, no _currentNode
        if (_currentChild == null) {
            // no children to process, we are done
            //System.Console.WriteLine($"[LSEventSequenceNode] No children to process for node {NodeID}, marking as SUCCESS.");
            // we keep processing the current node if available, otherwise we are done
            if (sequenceStatus != successStatus) {
                // this should never be the case
                System.Console.WriteLine($"[LSEventSequenceNode] Warning: Sequence node [{NodeID}] has no children to process but status is <{sequenceStatus}>");
            }
            return successStatus;
        }

        do {
            //System.Console.WriteLine($"[LSEventSequenceNode] Processing child node [{_currentChild.NodeID}].");

            // process the child
            var currentChildStatus = _currentChild.Process(context);
            sequenceStatus = GetNodeStatus();
            //System.Console.WriteLine($"[LSEventSequenceNode] Child node [{_currentChild.NodeID}] processed with status <{currentChildStatus}> sequenceStatus: <{sequenceStatus}>.");
            if (currentChildStatus == LSEventProcessStatus.WAITING) return LSEventProcessStatus.WAITING;
            if (sequenceStatus == LSEventProcessStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                //System.Console.WriteLine($"[LSEventSequenceNode] Warning: Sequence node [{NodeID}] is in WAITING state but child [{_currentChild.NodeID}] is not WAITING.");
                return LSEventProcessStatus.WAITING;
            }
            if (currentChildStatus == failureStatus || sequenceStatus == LSEventProcessStatus.CANCELLED) {
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
        return successStatus; //we make sure it return success, even if GetNodeStatus says otherwise, this could even be a case for unknown.
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
    /// This factory method provides a convenient way to create sequence nodes without directly
    /// invoking the protected constructor. The created node implements AND logic semantics.
    /// </remarks>
    public static LSEventSequenceNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, bool withInverter = false, params LSEventCondition?[] conditions) {
        return new LSEventSequenceNode(nodeID, order, priority, withInverter, conditions);
    }
}
