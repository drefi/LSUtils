using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

public class LSEventParallelNode : ILSEventLayerNode {
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");
    protected Dictionary<string, ILSEventNode> _children = new();
    protected Stack<ILSEventNode> _processStack = new();
    protected bool _isProcessing = false;
    protected int _successCount = 0;
    protected bool _isWaitingForChildren = false;

    public string NodeID { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Conditions { get; }
    public int NumRequiredToSucceed { get; }

    internal LSEventParallelNode(string nodeID, int numRequiredToSucceed, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        NodeID = nodeID;
        Priority = priority;
        NumRequiredToSucceed = numRequiredToSucceed;
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
    public ILSEventNode? FindChild(string label) {
        return _children.TryGetValue(label, out var child) ? child : null;
    }
    public ILSEventNode[] GetChildren() {
        return _children.Values.ToArray();
    }
    public bool HasChild(string label) {
        return _children.ContainsKey(label);
    }
    public bool RemoveChild(string label) {
        return _children.Remove(label);
    }
    public ILSEventLayerNode Clone() {
        var cloned = new LSEventParallelNode(NodeID, NumRequiredToSucceed, Priority, Conditions);
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
            _successCount = 0;
            _isWaitingForChildren = false;
        }
        // a stack allow to resume processing where it left off
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
            if (childStatus == LSEventProcessStatus.SUCCESS) _successCount++;
            if (childStatus == LSEventProcessStatus.WAITING) _isWaitingForChildren = true;
        }
        if (_isWaitingForChildren) {
            // if any child is still waiting, the whole node must wait
            return LSEventProcessStatus.WAITING;
        }

        if (_processStack.Count == 0) {
            // finished processing all children
            if (_successCount >= NumRequiredToSucceed) return LSEventProcessStatus.SUCCESS;
            return LSEventProcessStatus.FAILURE; // not enough successes means failure
        }

        throw new LSException("LSEventParallelNode in an invalid state after processing all children.");
    }

    public static LSEventParallelNode Create(string nodeID, int numRequiredToSucceed, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventParallelNode(nodeID, numRequiredToSucceed, priority, conditions);
    }

}
