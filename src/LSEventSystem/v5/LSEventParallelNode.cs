using System.Collections.Generic;
using System.Linq;
namespace LSUtils.EventSystem;

public class LSEventParallelNode : ILSEventLayerNode {
    int ILSEventNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");
    protected Dictionary<string, ILSEventNode> _children = new();
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
    public int NumRequiredToFailure { get; internal set; }

    internal LSEventParallelNode(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        NodeID = nodeID;
        Order = order;
        Priority = priority;
        NumRequiredToSucceed = numRequiredToSucceed;
        NumRequiredToFailure = numRequiredToFailure;
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
        var cloned = new LSEventParallelNode(NodeID, Order, NumRequiredToSucceed, NumRequiredToFailure, Priority, Conditions);
        foreach (var child in _children.Values) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    ILSEventNode ILSEventNode.Clone() => Clone();
    /// <summary>
    /// Gets the current status of the parallel node based on its children's statuses.
    /// The status priority is evaluated in the following order:
    /// </summary>
    /// <returns>The current status of the parallel node.</returns>
    /// <remarks>
    /// <para>The parallel node's status is determined by the first condition that applies:</para>
    /// <list type="bullet">
    ///     <item>
    ///         <term>Unknown ❓</term>
    ///         <description>No process has been started yet or cannot be determined.</description>
    ///     </item>
    ///     <item>
    ///         <term>Success ✅</term>
    ///         <description>No children are available (all filtered out by conditions).</description>
    ///     </item>
    ///     <item>
    ///         <term>Cancelled 🛑</term>
    ///         <description>Any child is <c>CANCELLED</c>.</description>
    ///     </item>
    ///     <item>
    ///         <term>Failure ❌</term>
    ///         <description>The minimum number of children defined by <c>NumRequiredToFailure</c> has failed.</description>
    ///     </item>
    ///     <item>
    ///         <term>Success ✅</term>
    ///         <description>The minimum number of children defined by <c>NumRequiredToSucceed</c> has succeeded.</description>
    ///     </item>
    ///     <item>
    ///         <term>Waiting ⏳</term>
    ///         <description>Any child is <c>WAITING</c>.</description>
    ///     </item>
    ///     <item>
    ///         <term>Success ✅</term>
    ///         <description>All children have succeeded.</description>
    ///     </item>
    ///     <item>
    ///         <term>Failure ❌</term>
    ///         <description>All children have failed.</description>
    ///     </item>
    /// </list>
    /// </remarks>
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

        int failureCount = childStatuses.Count(c => c == LSEventProcessStatus.FAILURE);
        if (NumRequiredToFailure > 0 && failureCount >= NumRequiredToFailure) {
            return LSEventProcessStatus.FAILURE;
        }
        int successCount = childStatuses.Count(c => c == LSEventProcessStatus.SUCCESS);
        if (NumRequiredToSucceed > 0 && successCount >= NumRequiredToSucceed) {
            return LSEventProcessStatus.SUCCESS;
        }
        if (childStatuses.Any(c => c == LSEventProcessStatus.WAITING)) {
            return LSEventProcessStatus.WAITING;
        }
        if (successCount >= NumRequiredToSucceed) {
            return LSEventProcessStatus.SUCCESS;
        }

        return LSEventProcessStatus.FAILURE;
    }
    public LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes) {
        // parallel can fail multiple nodes at a time so we need to care about nodes we want to fail.

        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to fail in parallel node {NodeID} failing all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                    System.Console.WriteLine($"[LSEventParallelNode] Failing child node {child.NodeID}.");
                    child.Fail(context, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var failedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (failedChild != null) {
                System.Console.WriteLine($"[LSEventParallelNode] Failing child node {failedChild.NodeID}.");
                failedChild.Fail(context, nodes);
            } else {
                System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to fail.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }
    public LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes) {
        // parallel can resume multiple nodes at a time so we need to care about nodes we want to resume.
        //compare nodes with _availableChildren
        if (nodes == null || nodes.Length == 0) {
            System.Console.WriteLine($"[LSEventParallelNode] No nodes specified to resume in parallel node {NodeID} resuming all childs.");
            foreach (var child in _availableChildren) {
                var childStatus = child.GetNodeStatus();
                if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                    System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {child.NodeID}.");
                    child.Resume(context, nodes);
                }
            }
            return GetNodeStatus(); // we return the current parallel status.
        }
        foreach (var node in nodes) {
            var resumedChild = _availableChildren.FirstOrDefault(c => c.NodeID == node);
            if (resumedChild != null) {
                System.Console.WriteLine($"[LSEventParallelNode] Resuming child node {resumedChild.NodeID}.");
                resumedChild.Resume(context, nodes);
            } else {
                System.Console.WriteLine($"[LSEventParallelNode] Node {NodeID} does not have a child with ID {node} to resume.");
            }
        }
        return GetNodeStatus(); // we return the current parallel status.
    }
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        foreach (var child in _availableChildren) {
            var childStatus = child.GetNodeStatus();
            if (childStatus == LSEventProcessStatus.WAITING || childStatus == LSEventProcessStatus.UNKNOWN) {
                System.Console.WriteLine($"[LSEventParallelNode] Cancelling child node {child.NodeID}.");
                child.Cancel(context);
            }
        }
        return LSEventProcessStatus.CANCELLED;
    }


    public LSEventProcessStatus Process(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventParallelNode] Process called on node {NodeID}, current status is {GetNodeStatus()}.");
        if (!LSEventConditions.IsMet(context.Event, this)) return LSEventProcessStatus.SUCCESS;


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
            var childStatus = child.Process(context);
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

    public static LSEventParallelNode Create(string nodeID, int order, int numRequiredToSucceed, int numRequiredToFailure, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventParallelNode(nodeID, order, numRequiredToSucceed, numRequiredToFailure, priority, conditions);
    }

}
