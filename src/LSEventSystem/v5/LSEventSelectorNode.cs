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

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventSelectorNode] Processing selector node {NodeID} with {_children.Count} children.");
        // check if the context is not cancelled, this also should not be needed but just to prevent weird behaviour
        if (context.IsCancelled) return LSEventProcessStatus.CANCELLED;

        // dont check node conditions, if the condition was suposed to not be met, the parent node should have skipped this node already
        // foreach (LSEventCondition condition in Conditions.GetInvocationList()) { //keeping this commented for clarity
        //     if (!condition(context.Event, this)) {
        //         return LSEventProcessStatus.SUCCESS; // skip if any conditions are not met
        //     }
        // }

        // create a stack to process children in order, this stack cannot be re-created if already processing
        // if for some reason this selector is called again and it should not been processed yet this means that the parent node should have cloned this node.
        if (_isProcessing == false) {
            // Initialize the stack with children ordered by Priority (critical first) and Order (lowest first)
            _processStack = new Stack<ILSEventNode>(_children.Values.OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse());
            _isProcessing = true;
            System.Console.WriteLine($"[LSEventSelectorNode] Initialized processing stack for node {NodeID}.");
        } else {
            System.Console.WriteLine($"[LSEventSelectorNode] Resuming processing stack for node {NodeID}.");
            // we do need to re-check if any child was set to SUCCESS while we were waiting
            if (_children.Values.Any(c => {
                var childKey = context.getNodeResultKey(c, this);
                context.registerProcessStatus(childKey, LSEventProcessStatus.UNKNOWN, out var existingStatus);
                return existingStatus == LSEventProcessStatus.SUCCESS;
            })) {
                System.Console.WriteLine($"[LSEventSelectorNode] A child node was already set to SUCCESS while waiting, selector node {NodeID} returning SUCCESS.");
                _processStack.Clear();
                return LSEventProcessStatus.SUCCESS;
            }
        }

        // while there are children in the stack
        while (_processStack.Count > 0) {
            // this whole test should not be needed
            // Resume can only be called during child.Process(context) or after it finishes (and the whole context.Process already is in a "waiting" status)
            // this means that no child should need to re-check if other child has SUCCESS status
            // 
            // foreach (var childNode in _children.Values.OrderByDescending(c => c.Priority).ThenBy(c => c.Order)) {
            //     var childNodeKey = context.getNodeResultKey(childNode, this);
            //     if (context.registerProcessStatus(childNodeKey, LSEventProcessStatus.UNKNOWN, out var existingStatus)) {
            //         // registrationResult true means status was updated, shouldn't happen with UNKNOWN
            //         continue;
            //     } else if (existingStatus == LSEventProcessStatus.SUCCESS) {
            //         // Child was resumed and is now SUCCESS, selector should succeed
            //         _processStack.Clear();
            //         return LSEventProcessStatus.SUCCESS;
            //     } else if (existingStatus == LSEventProcessStatus.CANCELLED) {
            //         // Child was cancelled
            //         _processStack.Clear();
            //         return LSEventProcessStatus.CANCELLED;
            //     }
            // }

            var child = _processStack.Pop();
            System.Console.WriteLine($"[LSEventSelectorNode] Processing child node {child.NodeID}.");

            // check child condition, if one is not met skip this child (all must be true)
            bool shouldSkip = false;
            foreach (LSEventCondition condition in child.Conditions.GetInvocationList()) {
                if (!condition(context.Event, child)) {
                    shouldSkip = true;
                    break;
                }
            }
            if (shouldSkip) {
                continue; // skip to next child
            }

            // create child NodeResultKey and save in the context (should not exist yet) with LSEventProcessStatus.UNKNOWN status
            var childKey = context.getNodeResultKey(child, this);
            if (context.registerProcessStatus(childKey, LSEventProcessStatus.UNKNOWN, out var childStatusExists) == false) {
                System.Console.WriteLine($"[LSEventSelectorNode] Child node {child.NodeID} status: {childStatusExists}.");
                if (childStatusExists != LSEventProcessStatus.UNKNOWN) return childStatusExists;
            }

            // process the child
            var childStatus = child.Process(context);
            System.Console.WriteLine($"[LSEventSelectorNode] Child node {child.NodeID} processed with status {childStatus}.");

            // check the context LSEventProcessStatus for this child:
            // if context result is different from UNKNOWN, return with the context result (early exit) 
            // **this means Resume/Fail was called while processing**
            if (context.registerProcessStatus(childKey, childStatus, out var updatedStatus)) {
                System.Console.WriteLine($"[LSEventSelectorNode] Child node {child.NodeID} status updated to {updatedStatus} by Resume/Fail.");
                // update context results failed, meaning the child was resumed/failed during the process (and before the return childStatus)
                // in this case we use the updated status from the context results
                childStatus = updatedStatus;
                // can only be SUCCESS or FAILURE here
                if (childStatus != LSEventProcessStatus.SUCCESS && childStatus != LSEventProcessStatus.FAILURE) {
                    System.Console.WriteLine($"[LSEventSelectorNode] Warning: Child node {child.NodeID} in unexpected state {childStatus} after resume/fail.");
                }
            }

            if (context.IsCancelled) {
                // If cancelled during processing, stop and return CANCELLED
                _processStack.Clear();
                return LSEventProcessStatus.CANCELLED;
            }
            System.Console.WriteLine($"[LSEventSelectorNode] Child node {child.NodeID} final status {childStatus}.");
            // if child result returns SUCCESS, WAITING, CANCELLED, return the child result
            if (childStatus == LSEventProcessStatus.SUCCESS) {
                // Clear the stack but keep _isProcessing to prevent reinitialization
                _processStack.Clear();
                return LSEventProcessStatus.SUCCESS; // Stop processing and return success
            } else if (childStatus == LSEventProcessStatus.WAITING) {
                return LSEventProcessStatus.WAITING; // Bubble up waiting
            } else if (childStatus == LSEventProcessStatus.CANCELLED) {
                // Clear the stack and return cancelled if child was cancelled
                _processStack.Clear();
                return LSEventProcessStatus.CANCELLED; // Stop processing and return cancelled
            }
            // if child result returns FAILURE, continue to next child
        }

        // If all children are processed without success, return FAILURE or if is empty return SUCCESS
        return _children.Count == 0 ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE;
    }

    public static LSEventSelectorNode Create(string nodeID, int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventSelectorNode(nodeID, order, priority, conditions);
    }
}
