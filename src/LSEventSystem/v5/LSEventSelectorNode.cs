using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;
/// <summary>
/// An event node that processes its children in order until one succeeds.
/// </summary>
public class LSEventSelectorNode : ILSEventLayerNode {
    protected Dictionary<string, ILSEventNode> _children = new();
    protected Stack<ILSEventNode> _processStack = new();
    protected bool _isProcessing = false;
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

    public string NodeID { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Conditions { get; }

    protected LSEventSelectorNode(string nodeId, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        NodeID = nodeId;
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

    public ILSEventNode? FindChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }

    public bool HasChild(string label) => _children.ContainsKey(label);

    public ILSEventNode[] GetChildren() => _children.Values.ToArray();

    public ILSEventLayerNode Clone() {
        var cloned = new LSEventSelectorNode(NodeID, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) return LSEventProcessStatus.CANCELLED;

        foreach (LSEventCondition condition in Conditions.GetInvocationList()) {
            if (!condition(context.Event, this)) {
                return LSEventProcessStatus.SUCCESS; // skip if any conditions are not met
            }
        }
        if (_isProcessing == false) {
            _processStack = new Stack<ILSEventNode>(_children.Values.OrderByDescending(c => c.Priority));
            _isProcessing = true;
        }
        while (_processStack.Count > 0) {
            var child = _processStack.Pop();
            var childStatus = child.Process(context);
            var childKey = context.getNodeResultKey(child);
            if (!context.registerProcessStatus(childKey, childStatus, out var updatedStatus)) {
                // registration failed, meaning the child was not in a state that could be processed
                // this can happen if the child was resumed/failed while processing, in this case we use the updated status
                childStatus = updatedStatus;
            }
            if (context.IsCancelled) {
                // If cancelled during processing, stop and return CANCELLED
                _processStack.Clear();
                return LSEventProcessStatus.CANCELLED;
            }
            if (childStatus == LSEventProcessStatus.SUCCESS) {
                // to prevent unexpected behavior, we clear the stack but don't reset _isProcessing
                _processStack.Clear();
                return LSEventProcessStatus.SUCCESS; // Stop processing and return success
            } else if (childStatus == LSEventProcessStatus.WAITING) {
                return LSEventProcessStatus.WAITING; // Bubble up waiting
            }
        }
        // if reach this mean all children failed
        return LSEventProcessStatus.FAILURE;
    }

    public static LSEventSelectorNode Create(string nodeID, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSelectorNode(nodeID, priority, conditions);
    }
}
