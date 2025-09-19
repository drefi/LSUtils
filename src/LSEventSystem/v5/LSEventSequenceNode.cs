using System;
using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;
/// <summary>
/// An event node that processes its children in order until one fails.
/// </summary>
public class LSEventSequenceNode : ILSEventLayerNode {
    protected Dictionary<string, ILSEventNode> _children = new();
    ILSEventNode? _currentChild;
    protected Stack<ILSEventNode> _processStack = new();
    //protected Dictionary<ILSEventNode, LSEventProcessStatus> _childrenStatuses = new();
    List<ILSEventNode> _availableChildren = new();
    protected bool _isProcessing = false;
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

    public string NodeID { get; }
    public LSPriority Priority { get; }
    public int Order { get; }
    public LSEventCondition Conditions { get; }

    protected LSEventSequenceNode(string nodeId, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
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
        if (_isProcessing) {
            throw new System.InvalidOperationException("Cannot add child after processing.");
        }
        _children[child.NodeID] = child;
    }

    public bool RemoveChild(string label) {
        if (_isProcessing) {
            throw new System.InvalidOperationException("Cannot remove child after processing.");
        }
        return _children.Remove(label);
    }

    public ILSEventNode? GetChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    public bool HasChild(string label) => _children.ContainsKey(label);

    public ILSEventNode[] GetChildren() => _children.Values.ToArray();

    public ILSEventLayerNode Clone() {
        var cloned = new LSEventSequenceNode(NodeID, Order, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

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
        if (childStatuses.Any(c => c == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING; // we have at least one child that is still waiting
        }
        // check for FAILURE has the third highest priority
        if (childStatuses.Any(c => c == LSEventProcessStatus.FAILURE)) {
            return LSEventProcessStatus.FAILURE; // we do not need to continue processing
        }
        return LSEventProcessStatus.SUCCESS;
    }
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // sequence can only fail one node at a time so we don't need to care about nodes we just fail the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            System.Console.WriteLine($"[LSEventSequenceNode] Failing waiting child node {waitingChild.NodeID}.");
            return waitingChild.Fail(context, nodes); //we propagate the fail to the waiting child
        }
        System.Console.WriteLine($"[LSEventSequenceNode] No child node is in WAITING state, cannot fail sequence node {NodeID}.");


        return GetNodeStatus(); // we return the current sequence status.
    }
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        return _currentChild?.Cancel(context) ?? LSEventProcessStatus.CANCELLED;
    }


    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // sequence can only resume one node at a time so we don't need to care about nodes we just resume the first 

        var waitingChild = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList().FirstOrDefault();
        if (waitingChild != null) {
            System.Console.WriteLine($"[LSEventSequenceNode] Resuming waiting child node {waitingChild.NodeID}.");
            return waitingChild.Resume(context, nodes); //we propagate the resume to the waiting child
        }
        System.Console.WriteLine($"[LSEventSequenceNode] No child node is in WAITING state, cannot resume sequence node {NodeID}.");
        return GetNodeStatus(); //we return the current sequence status, it may be SUCCESS if all children are done
    }

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventSequenceNode] Processing sequence node [{NodeID}]");
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
            System.Console.WriteLine($"[LSEventSequenceNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()} {string.Join(", ", Array.ConvertAll(_availableChildren.ToArray(), c => c.NodeID))} _currentChild: {_currentChild?.NodeID}.");
        }
        // success condition: all children have been processed, no _currentNode
        if (_currentChild == null) {
            // no children to process, we are done
            System.Console.WriteLine($"[LSEventSequenceNode] No children to process for node {NodeID}, marking as SUCCESS.");
            // we keep processing the current node if available, otherwise we are done
            if (sequenceStatus != LSEventProcessStatus.SUCCESS) {
                // this should never be the case
                System.Console.WriteLine($"[LSEventSequenceNode] Warning: Sequence node [{NodeID}] has no children to process but status is <{sequenceStatus}>");
            }
            return LSEventProcessStatus.SUCCESS;
        }

        do {
            System.Console.WriteLine($"[LSEventSequenceNode] Processing child node [{_currentChild.NodeID}].");

            // process the child
            var currentChildStatus = _currentChild.Process(context);
            sequenceStatus = GetNodeStatus();
            System.Console.WriteLine($"[LSEventSequenceNode] Child node [{_currentChild.NodeID}] processed with status <{currentChildStatus}> sequenceStatus: <{sequenceStatus}>.");
            if (currentChildStatus == LSEventProcessStatus.WAITING) return LSEventProcessStatus.WAITING;
            if (sequenceStatus == LSEventProcessStatus.WAITING) {
                // this should never happen because childStatus is WAITING should have been caught above
                System.Console.WriteLine($"[LSEventSequenceNode] Warning: Sequence node [{NodeID}] is in WAITING state but child [{_currentChild.NodeID}] is not WAITING.");
                return LSEventProcessStatus.WAITING;
            }
            if (currentChildStatus == LSEventProcessStatus.FAILURE || sequenceStatus == LSEventProcessStatus.CANCELLED) {
                // exit condition in this node is clear the stack and current node
                System.Console.WriteLine($"[LSEventSequenceNode] Ué Sequence node [{NodeID}] finished processing because child [{_currentChild.NodeID}] returned <{currentChildStatus}>.");
                _processStack.Clear();
                _currentChild = null;
                return sequenceStatus; // propagate the sequence status
            }

            // get the next node
            if (_processStack.Count == 0) {
                System.Console.WriteLine($"[LSEventSequenceNode] Sequence node [{NodeID}] has processed all children.");
                _currentChild = null; // we are done
                break;
            }
            _currentChild = _processStack.Pop();
        } while (_currentChild != null);

        //reach this point means that the sequence was successfull
        System.Console.WriteLine($"[LSEventSequenceNode] Sequence node [{NodeID}] finished processing all children. Final status: [{sequenceStatus}]");
        return LSEventProcessStatus.SUCCESS; //we make sure it return success, even if GetNodeStatus says otherwise, this could even be a case for unknown.
    }

    public static LSEventSequenceNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSequenceNode(nodeID, order, priority, conditions);
    }
}
