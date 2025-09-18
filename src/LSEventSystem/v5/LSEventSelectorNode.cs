using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;
/// <summary>
/// An event node that processes its children in order until one succeeds.
/// </summary>
public class LSEventSelectorNode : ILSEventLayerNode {
    protected Dictionary<string, ILSEventNode> _children = new();
    ILSEventNode? _currentChild;
    protected Stack<ILSEventNode> _processStack = new();
    List<ILSEventNode> _availableChildren = new();
    protected bool _isProcessing = false;
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

    public string NodeID { get; }
    public LSPriority Priority { get; }
    public int Order { get; }
    public LSEventCondition Conditions { get; }

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

    public void AddChild(ILSEventNode child) {
        _children[child.NodeID] = child;
    }
    public bool RemoveChild(string label) {
        return _children.Remove(label);
    }

    public ILSEventNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    public bool HasChild(string label) => _children.ContainsKey(label);

    public ILSEventNode[] GetChildren() => _children.Values.ToArray();

    public ILSEventLayerNode Clone() {
        var cloned = new LSEventSelectorNode(NodeID, Order, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

    public LSEventProcessStatus GetNodeStatus() {
        // Selector node status logic:
        // - If any child is in SUCCESS, the selector is in SUCCESS.
        // - If all children are in FAILURE, the selector is in FAILURE.
        // - If any child is in WAITING, the selector is in WAITING.
        // - If any child is in CANCELLED, the selector is in CANCELLED.
        // - if there are no children, the selector is in SUCCESS.
        if (_children.Count == 0) return LSEventProcessStatus.SUCCESS;

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
    public void Cancel(LSEventProcessContext context) {
        _currentChild?.Cancel(context);
    }


    public LSEventProcessStatus Process(LSEventProcessContext context, params string[]? nodes) {
        if (LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;

        var selectorStatus = GetNodeStatus();

        System.Console.WriteLine($"[LSEventSelectorNode] Processing selector node {NodeID} with {_children.Count} children. Current status: [{selectorStatus}]");

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
            System.Console.WriteLine($"[LSEventSelectorNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()}.");
        }
        // success condition: any child has succeeded or no children to process
        if (_currentChild == null || _processStack.Count == 0) {
            // no children to process, we are done
            System.Console.WriteLine($"[LSEventSelectorNode] No children to process for node {NodeID}, checking final status. selectorStatus {selectorStatus}");
            return selectorStatus; // return selector status
        }

        do {
            System.Console.WriteLine($"[LSEventSelectorNode] Processing child node {_currentChild.NodeID}.");
            // no more need to check for condition, we already filtered children that meet conditions during stack initialization

            // process the child
            var currentChildStatus = _currentChild.Process(context, _currentChild.NodeID); //child status will only be used to update the selector state
            selectorStatus = GetNodeStatus();
            if (currentChildStatus == LSEventProcessStatus.WAITING) return LSEventProcessStatus.WAITING;
            if (selectorStatus == LSEventProcessStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                System.Console.WriteLine($"[LSEventSelectorNode] Warning: Selector node {NodeID} is in WAITING state but child {_currentChild.NodeID} is not WAITING.");
                return LSEventProcessStatus.WAITING;
            }
            if (currentChildStatus == LSEventProcessStatus.SUCCESS || selectorStatus == LSEventProcessStatus.CANCELLED) {
                // exit condition: child succeeded (selector success) or cancelled
                _processStack.Clear();
                _currentChild = null;
                return selectorStatus; // propagate the selector status
            }

            System.Console.WriteLine($"[LSEventSelectorNode] Child node {_currentChild.NodeID} processed with status {currentChildStatus} selectorStatus: {selectorStatus}.");
            // get the next node if child failed (continue trying other children)
            if (_processStack.Count > 0) {
                _currentChild = _processStack.Pop();
            } else {
                _currentChild = null; // no more children
            }
        } while (_currentChild != null);

        // reach this point means that all children failed
        System.Console.WriteLine($"[LSEventSelectorNode] Selector node {NodeID} finished processing all children. All failed, marking as FAILURE.");
        return LSEventProcessStatus.FAILURE; // all children failed, selector fails
    }

    public static LSEventSelectorNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSelectorNode(nodeID, order, priority, conditions);
    }
}
