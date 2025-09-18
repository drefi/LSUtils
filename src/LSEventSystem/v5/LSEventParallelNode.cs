using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

public class LSEventParallelNode : ILSEventLayerNode {
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");
    protected Dictionary<string, ILSEventNode> _children = new();
    ILSEventNode? _currentChild;
    protected Stack<ILSEventNode> _processStack = new();
    List<ILSEventNode> _availableChildren = new();
    protected bool _isProcessing = false;
    // protected int _successCount = 0;
    // protected bool _isWaitingForChildren = false;

    public string NodeID { get; }
    public LSPriority Priority { get; }
    public int Order { get; }
    public LSEventCondition Conditions { get; }
    public int NumRequiredToSucceed { get; internal set; }

    internal LSEventParallelNode(string nodeID, int order, int numRequiredToSucceed, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        NodeID = nodeID;
        Order = order;
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
    public ILSEventNode? GetChild(string label) {
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
        var cloned = new LSEventParallelNode(NodeID, Order, NumRequiredToSucceed, Priority, Conditions);
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

        // Check for WAITING has the second highest priority
        if (childStatuses.Any(c => c == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING;
        }

        // we can't have parallel status unknown after we start processing, all child must return either a final status or WAITING
        // if (childStatuses.Any(c => c == LSEventProcessStatus.UNKNOWN)) {
        //     return LSEventProcessStatus.UNKNOWN;
        // }

        // Count successes
        int successCount = childStatuses.Count(c => c == LSEventProcessStatus.SUCCESS);
        return successCount >= NumRequiredToSucceed ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE;
    }
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // parallel can fail multiple nodes at a time so we need to care about nodes we want to fail.
        var waitingChildren = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList();
        waitingChildren.ForEach(waitingChild => {
            if (waitingChild != null) {
                System.Console.WriteLine($"[LSEventParallelNode] Failing waiting child node {waitingChild.NodeID}.");
                waitingChild.Fail(context, nodes);
            }
        });
        if (waitingChildren.Count == 0) {
            System.Console.WriteLine($"[LSEventParallelNode] No child node is in WAITING state, cannot fail parallel node {NodeID}.");
        }

        return GetNodeStatus(); // we return the current parallel status.
    }
    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // parallel can resume multiple nodes at a time so we need to care about nodes we want to resume.
        var waitingChildren = _availableChildren.Where(c => c.GetNodeStatus() == LSEventProcessStatus.WAITING).ToList();
        waitingChildren.ForEach(waitingChild => {
            if (waitingChild != null) {
                System.Console.WriteLine($"[LSEventParallelNode] Resuming waiting child node {waitingChild.NodeID}.");
                waitingChild.Resume(context, nodes);
            }
        });
        if (waitingChildren.Count == 0) {
            System.Console.WriteLine($"[LSEventParallelNode] No child node is in WAITING state, cannot resume parallel node {NodeID}.");
        }

        return GetNodeStatus(); //we return the current parallel status
    }
    public void Cancel(LSEventProcessContext context) {
        _currentChild?.Cancel(context);
    }


    public LSEventProcessStatus Process(LSEventProcessContext context, params string[]? nodes) {
        if (LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;


        System.Console.WriteLine($"[LSEventParallelNode] Processing parallel node {NodeID} with {_children.Count} children.");
        var parallelStatus = GetNodeStatus();

        // Initialize available children list if not done yet
        if (_isProcessing == false) {
            // will only process children that meet conditions
            // children ordered by Priority (critical first) and Order (lowest first)
            _availableChildren = _children.Values.Where(c => LSEventConditions.IsMet(context.Event, c)).OrderByDescending(c => c.Priority).ThenBy(c => c.Order).Reverse().ToList();
            _isProcessing = true;
            System.Console.WriteLine($"[LSEventParallelNode] Initialized processing for node {NodeID}, children: {_availableChildren.Count()}.");
        }

        // exit condition for parallel is if all children are processed
        if (parallelStatus != LSEventProcessStatus.UNKNOWN) {
            System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} already has final status {parallelStatus}, skipping processing.");
            return parallelStatus;
        }


        foreach (var child in _availableChildren) {
            System.Console.WriteLine($"[LSEventParallelNode] Processing child node {child.NodeID}.");

            // Process the child
            var childStatus = child.Process(context, child.NodeID);
            System.Console.WriteLine($"[LSEventParallelNode] Child node {child.NodeID} processed with status {childStatus}.");

        }

        parallelStatus = GetNodeStatus();
        if (parallelStatus == LSEventProcessStatus.SUCCESS || parallelStatus == LSEventProcessStatus.FAILURE || parallelStatus == LSEventProcessStatus.CANCELLED) {
            System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} completed with status {parallelStatus}.");
            return parallelStatus;
        }


        // Still processing
        System.Console.WriteLine($"[LSEventParallelNode] Parallel node {NodeID} still processing parallelStatus: {parallelStatus}.");
        return parallelStatus;
    }

    public static LSEventParallelNode Create(string nodeID, int order, int numRequiredToSucceed, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventParallelNode(nodeID, order, numRequiredToSucceed, priority, conditions);
    }

}
