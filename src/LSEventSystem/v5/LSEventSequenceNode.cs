using System;
using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;
/// <summary>
/// An event node that processes its children in order until one fails.
/// </summary>
public class LSEventSequenceNode : ILSEventLayerNode {
    protected Dictionary<string, ILSEventNode> _children = new();
    protected Stack<ILSEventNode> _processStack = new();
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

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) return LSEventProcessStatus.CANCELLED;

        if (_isProcessing == false) {
            // Initialize the stack with children ordered by Priority (critical first) and Order (lowest first).
            // since this is a stack maybe we should reverse the order, not sure...
            _processStack = new Stack<ILSEventNode>(_children.Values.OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse());
            _isProcessing = true;
        }
        while (_processStack.Count > 0) {
            var child = _processStack.Pop();
            //child condition check
            bool shouldSkip = false;
            foreach (LSEventCondition condition in child.Conditions.GetInvocationList()) {
                // skip if any conditions are not met
                if (!condition(context.Event, child)) {
                    shouldSkip = true;
                    break;
                }
            }
            if (shouldSkip) {
                continue; // skip to next child
            }
            // initialize the child key
            var childKey = context.getNodeResultKey(child, this);
            context.registerProcessStatus(childKey, LSEventProcessStatus.UNKNOWN, out _);

            var childStatus = child.Process(context);
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
            if (childStatus == LSEventProcessStatus.FAILURE) {
                // Clear the stack but keep _isProcessing to prevent reinitialization
                _processStack.Clear();
                return LSEventProcessStatus.FAILURE; // Stop processing and return failure
            } else if (childStatus == LSEventProcessStatus.CANCELLED) {
                // Clear the stack and return cancelled if child was cancelled
                _processStack.Clear();
                return LSEventProcessStatus.CANCELLED; // Stop processing and return cancelled
            } else if (childStatus == LSEventProcessStatus.WAITING) {
                return LSEventProcessStatus.WAITING; // Bubble up waiting
            }
        }
        // if we reach here, all children processed successfully
        return LSEventProcessStatus.SUCCESS;
    }

    public static LSEventSequenceNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSequenceNode(nodeID, order, priority, conditions);
    }
}
