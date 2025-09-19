using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

/// <summary>
/// Layer node implementation that processes children sequentially with OR logic semantics.
/// Succeeds when any child succeeds, fails only when all children fail.
/// Follows the unified layer node processing pattern established by LSEventSequenceNode.
/// </summary>
/// <remarks>
/// <para><strong>Processing Semantics (OR Logic):</strong></para>
/// <list type="bullet">
/// <item><description><strong>Success Condition</strong>: Any child success immediately succeeds the entire selector</description></item>
/// <item><description><strong>Failure Condition</strong>: All children must fail for the selector to fail</description></item>
/// <item><description><strong>Processing Order</strong>: Children processed sequentially by Priority (descending) then Order (ascending)</description></item>
/// <item><description><strong>Short-Circuit</strong>: Processing stops immediately on first success or cancellation</description></item>
/// </list>
/// 
/// <para><strong>Unified Processing Pattern:</strong></para>
/// <para>This class implements the same pattern as LSEventSequenceNode but with inverted success/failure logic:</para>
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
/// <item><description><strong>SUCCESS</strong>: Any child succeeded or no eligible children</description></item>
/// <item><description><strong>FAILURE</strong>: All children failed</description></item>
/// <item><description><strong>WAITING</strong>: Any child is waiting for external completion</description></item>
/// <item><description><strong>CANCELLED</strong>: Any child was cancelled</description></item>
/// </list>
/// 
/// <para><strong>Selector vs Sequence Comparison:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Selector (OR)</strong>: Continues processing until any child succeeds or all fail</description></item>
/// <item><description><strong>Sequence (AND)</strong>: Continues processing until any child fails or all succeed</description></item>
/// <item><description><strong>Both</strong>: Use identical processing infrastructure with different success/failure logic</description></item>
/// </list>
/// </remarks>
/// <summary>
/// An event node that processes its children in order until one succeeds.
/// </summary>
public class LSEventSelectorNode : ILSEventLayerNode {
    /// <summary>
    /// Dictionary storing child nodes keyed by their NodeID for O(1) lookup operations.
    /// </summary>
    protected Dictionary<string, ILSEventNode> _children = new();
    
    /// <summary>
    /// Current child node being processed. Null when no processing is active or processing is complete.
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

    /// <summary>
    /// Initializes a new selector node with the specified configuration.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this selector node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <remarks>
    /// <para><strong>Condition Handling:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Default</strong>: If no conditions provided, uses a default condition that always returns true</description></item>
    /// <item><description><strong>Composition</strong>: Multiple conditions are combined using delegate composition (+=)</description></item>
    /// <item><description><strong>Null Safety</strong>: Null conditions in the array are automatically filtered out</description></item>
    /// </list>
    /// </remarks>
    protected LSEventSelectorNode(string nodeId, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        NodeID = nodeId;
        Order = order;
        Priority = priority;
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
    /// Adds a child node to this selector node's collection.
    /// </summary>
    /// <param name="child">The child node to add. Must have a unique NodeID within this selector.</param>
    /// <remarks>
    /// <para><strong>No Processing Restriction:</strong> Unlike sequence nodes, selectors allow child modification during processing.</para>
    /// <para><strong>Uniqueness:</strong> If a child with the same NodeID already exists, it will be replaced.</para>
    /// </remarks>
    public void AddChild(ILSEventNode child) {
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
    public ILSEventNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    /// <inheritdoc />
    public bool HasChild(string label) => _children.ContainsKey(label);

    /// <inheritdoc />
    public ILSEventNode[] GetChildren() => _children.Values.ToArray();

    /// <inheritdoc />
    public ILSEventLayerNode Clone() {
        var cloned = new LSEventSelectorNode(NodeID, Order, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

    /// <summary>
    /// Gets the current processing status by aggregating child node statuses according to selector logic.
    /// </summary>
    /// <returns>The current status of this selector node.</returns>
    /// <remarks>
    /// <para><strong>Status Evaluation Priority (selector OR logic):</strong></para>
    /// <list type="number">
    /// <item><description><strong>SUCCESS</strong>: No eligible children (all filtered out by conditions)</description></item>
    /// <item><description><strong>CANCELLED</strong>: Any child is in CANCELLED state (highest priority)</description></item>
    /// <item><description><strong>SUCCESS</strong>: Any child is in SUCCESS state (selector succeeds immediately)</description></item>
    /// <item><description><strong>WAITING</strong>: Any child is in WAITING state (blocks further processing)</description></item>
    /// <item><description><strong>FAILURE</strong>: All children are in FAILURE state (selector fails only when all fail)</description></item>
    /// <item><description><strong>UNKNOWN</strong>: Cannot determine status (default fallback)</description></item>
    /// </list>
    /// 
    /// <para><strong>OR Logic Differences from Sequence:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Priority</strong>: Any success immediately succeeds the selector</description></item>
    /// <item><description><strong>Failure Requirement</strong>: ALL children must fail for selector to fail</description></item>
    /// <item><description><strong>Early Termination</strong>: First success stops processing, unlike sequence which continues until failure</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus GetNodeStatus() {
        // Selector node status logic:
        // - If any child is in SUCCESS, the selector is in SUCCESS.
        // - If all children are in FAILURE, the selector is in FAILURE.
        // - If any child is in WAITING, the selector is in WAITING.
        // - If any child is in CANCELLED, the selector is in CANCELLED.
        // - if there are no children, the selector is in SUCCESS.
        if (_availableChildren.Count == 0) return LSEventProcessStatus.SUCCESS;

        // check for CANCELLED has the highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSEventProcessStatus.CANCELLED)) {
            return LSEventProcessStatus.CANCELLED; // we have nothing to process anymore
        }
        // check for SUCCESS has the second highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSEventProcessStatus.SUCCESS)) {
            return LSEventProcessStatus.SUCCESS; // we found a successful child
        }
        // check for WAITING has the third highest priority
        if (_availableChildren.Any(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING; // we have at least one child that is still waiting
        }
        // check if all children have failed, if so the selector is FAILURE, otherwise cannot be determined
        return _availableChildren.Count == _availableChildren.Count(c => c.GetNodeStatus() == LSEventProcessStatus.FAILURE) ? LSEventProcessStatus.FAILURE : LSEventProcessStatus.UNKNOWN;
    }

    /// <summary>
    /// Propagates failure to waiting children according to selector logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for failure.</param>
    /// <returns>The current processing status after the failure operation.</returns>
    /// <remarks>
    /// <para><strong>Selector Failure Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>First Waiting Child</strong>: Fails the first child found in WAITING state</description></item>
    /// <item><description><strong>Propagation</strong>: Delegates failure to the waiting child for proper handling</description></item>
    /// <item><description><strong>OR Logic</strong>: Selector fails only when all children have failed</description></item>
    /// <item><description><strong>Status Return</strong>: Returns current selector status after failure propagation</description></item>
    /// </list>
    /// 
    /// <para>If no children are waiting, returns the current selector status without modification.</para>
    /// </remarks>
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // selector can only fail one node at a time so we don't need to care about nodes we just fail the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            System.Console.WriteLine($"[LSEventSelectorNode] Failing waiting child node {waitingChild.NodeID}.");
            return waitingChild.Fail(context, nodes); //we propagate the fail to the waiting child
        }
        System.Console.WriteLine($"[LSEventSelectorNode] No child node is in WAITING state, cannot fail selector node {NodeID}.");

        return GetNodeStatus(); // we return the current selector status.
    }

    /// <summary>
    /// Propagates resumption to waiting children according to selector logic.
    /// </summary>
    /// <param name="context">The processing context for delegation and cancellation handling.</param>
    /// <param name="nodes">Optional array of specific node IDs to target for resumption.</param>
    /// <returns>The current processing status after the resume operation.</returns>
    /// <remarks>
    /// <para><strong>Selector Resume Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>First Waiting Child</strong>: Resumes the first child found in WAITING state</description></item>
    /// <item><description><strong>Propagation</strong>: Delegates resumption to the waiting child for continued processing</description></item>
    /// <item><description><strong>OR Logic</strong>: First child to succeed will make the entire selector succeed</description></item>
    /// <item><description><strong>Status Return</strong>: Returns current selector status after resume propagation</description></item>
    /// </list>
    /// 
    /// <para>If no children are waiting, returns the current selector status which may be SUCCESS if any child succeeded.</para>
    /// </remarks>
    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // selector can only resume one node at a time so we don't need to care about nodes we just resume the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            System.Console.WriteLine($"[LSEventSelectorNode] Resuming waiting child node {waitingChild.NodeID}.");
            return waitingChild.Resume(context, nodes); //we propagate the resume to the waiting child
        }
        System.Console.WriteLine($"[LSEventSelectorNode] No child node is in WAITING state, cannot resume selector node {NodeID}.");
        return GetNodeStatus(); //we return the current selector status, it may be SUCCESS if any child succeeded
    }

    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        return _currentChild?.Cancel(context) ?? LSEventProcessStatus.CANCELLED;
    }

    /// <summary>
    /// Processes this selector node using sequential OR logic until the first child succeeds.
    /// </summary>
    /// <param name="context">The processing context containing the event and execution environment.</param>
    /// <returns>The final processing status of this selector node.</returns>
    /// <remarks>
    /// <para><strong>Selector Processing Algorithm (OR Logic):</strong></para>
    /// <list type="number">
    /// <item><description><strong>Condition Check</strong>: If node conditions are not met, returns SUCCESS immediately</description></item>
    /// <item><description><strong>Initialization</strong>: On first call, filters and sorts children by conditions, priority, and order</description></item>
    /// <item><description><strong>Sequential Processing</strong>: Processes children one by one in priority order</description></item>
    /// <item><description><strong>Early Success</strong>: Returns SUCCESS immediately when any child succeeds</description></item>
    /// <item><description><strong>Continue on Failure</strong>: Moves to next child when current child fails</description></item>
    /// <item><description><strong>Final Failure</strong>: Returns FAILURE only when all children have failed</description></item>
    /// </list>
    /// 
    /// <para><strong>State Management:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>_isProcessing</strong>: Set to true after first initialization, never reset</description></item>
    /// <item><description><strong>_processStack</strong>: Contains children in reverse priority order for sequential processing</description></item>
    /// <item><description><strong>_currentChild</strong>: Points to the currently processing child node</description></item>
    /// <item><description><strong>_availableChildren</strong>: Filtered and sorted list of eligible children</description></item>
    /// </list>
    /// 
    /// <para><strong>OR Logic vs Sequence Logic:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Success Condition</strong>: ANY child success (vs ALL children success in sequence)</description></item>
    /// <item><description><strong>Failure Condition</strong>: ALL children fail (vs ANY child failure in sequence)</description></item>
    /// <item><description><strong>Early Termination</strong>: Stops on first success (vs stops on first failure in sequence)</description></item>
    /// </list>
    /// 
    /// <para><strong>Cancellation and Waiting:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>WAITING</strong>: Returned immediately if current child is waiting</description></item>
    /// <item><description><strong>CANCELLED</strong>: Terminates processing and clears remaining stack</description></item>
    /// <item><description><strong>State Preservation</strong>: Processing state maintained across multiple calls</description></item>
    /// </list>
    /// </remarks>
    public LSEventProcessStatus Process(LSEventProcessContext context) {
        var selectorStatus = GetNodeStatus();
        System.Console.WriteLine($"[LSEventSelectorNode] Processing selector node [{NodeID}] selectorStatus: <{selectorStatus}>");
        if (!LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;


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
            System.Console.WriteLine($"[LSEventSelectorNode] Initialized processing for node [{NodeID}], children: {_availableChildren.Count()} _currentChild: {_currentChild?.NodeID}.");
        }
        // success condition: any child has succeeded or no children to process
        if (_currentChild == null) {
            // no children to process, we are done
            System.Console.WriteLine($"[LSEventSelectorNode] No children to process for node [{NodeID}], checking final status. selectorStatus {selectorStatus}");
            return selectorStatus; // return selector status
        }

        do {
            System.Console.WriteLine($"[LSEventSelectorNode] Processing child node [{_currentChild.NodeID}].");
            // no more need to check for condition, we already filtered children that meet conditions during stack initialization

            // process the child
            var currentChildStatus = _currentChild.Process(context); //child status will only be used to update the selector state
            selectorStatus = GetNodeStatus();
            System.Console.WriteLine($"[LSEventSelectorNode] Child node [{_currentChild.NodeID}] processed with status <{currentChildStatus}> selectorStatus: <{selectorStatus}>.");
            if (currentChildStatus == LSEventProcessStatus.WAITING) return LSEventProcessStatus.WAITING;
            if (selectorStatus == LSEventProcessStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                System.Console.WriteLine($"[LSEventSelectorNode] Warning: Selector node [{NodeID}] is in WAITING state but child [{_currentChild.NodeID}] is not WAITING.");
                return LSEventProcessStatus.WAITING;
            }
            if (currentChildStatus == LSEventProcessStatus.SUCCESS || selectorStatus == LSEventProcessStatus.CANCELLED) {
                // exit condition: child succeeded (selector success) or cancelled
                System.Console.WriteLine($"[LSEventSelectorNode] Selector node [{NodeID}] finished processing because child [{_currentChild.NodeID}] returned {currentChildStatus}.");
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
        System.Console.WriteLine($"[LSEventSelectorNode] Selector node [{NodeID}] finished processing all children. All failed, marking as FAILURE.");
        return LSEventProcessStatus.FAILURE; // all children failed, selector fails
    }

    /// <summary>
    /// Creates a new selector node with the specified configuration.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the selector node.</param>
    /// <param name="order">Execution order among sibling nodes with the same priority.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>A new configured selector node instance.</returns>
    /// <remarks>
    /// <para><strong>Factory Method Benefits:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Convenience</strong>: Provides public access to protected constructor</description></item>
    /// <item><description><strong>Clarity</strong>: Explicit factory method name indicates intent</description></item>
    /// <item><description><strong>Consistency</strong>: Matches factory pattern used across the framework</description></item>
    /// </list>
    /// </remarks>
    public static LSEventSelectorNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSelectorNode(nodeID, order, priority, conditions);
    }
}
