using System.Linq;

namespace LSUtils.EventSystem;

public class LSEventHandlerNode : ILSEventNode {
    protected LSEventHandlerNode? _baseNode;
    protected int _executionCount = 0;
    protected LSEventHandler _handler;
    public string NodeID { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Conditions { get; }
    public int ExecutionCount {
        get => _baseNode?.ExecutionCount ?? _executionCount;
        internal set {
            if (_baseNode != null) {
                _baseNode.ExecutionCount = value;
            } else {
                _executionCount = value;
            }
        }
    }
    // Order is set during registration in the event system, starts at 0 and increases with each new node registered
    public int Order { get; }
    protected LSEventHandlerNode(string nodeID, LSEventHandler handler,
                      int order, LSPriority priority = LSPriority.NORMAL, LSEventHandlerNode? baseNode = null, params LSEventCondition?[] conditions) {
        _baseNode = baseNode;
        NodeID = nodeID;
        Priority = priority;
        _handler = handler;
        Order = order;
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

    public LSEventProcessStatus GetNodeStatus() {
        return _nodeStatus;
    }
    LSEventProcessStatus _nodeStatus = LSEventProcessStatus.UNKNOWN;

    protected bool updateNodeStatus(LSEventProcessStatus status, out LSEventProcessStatus previousStatus) {
        System.Console.WriteLine($"[LSEventHandlerNode] Updating node {NodeID} status from [{_nodeStatus}] to [{status}].");
        previousStatus = _nodeStatus;
        if (status == LSEventProcessStatus.UNKNOWN) {
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler attempted to update status to UNKNOWN.");
            return false; // never update to UNKNOWN
        }
        if (previousStatus == LSEventProcessStatus.UNKNOWN || previousStatus == LSEventProcessStatus.WAITING) {
            _nodeStatus = status;
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler was updated successfully to {status}.");
            return true;
        }
        return false;
    }

    LSEventProcessStatus ILSEventNode.Process(LSEventProcessContext context, params string[]? nodes) {
        // we should not need to check for the condition because only handler nodes that meet the condition are processed

        // node handler exit condition (because when WAITING we keep processing it)
        if (_nodeStatus != LSEventProcessStatus.UNKNOWN && _nodeStatus != LSEventProcessStatus.WAITING) {
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler already has final status {_nodeStatus}");
            return _nodeStatus; // already completed
        }

        // increment execution count indicating the handler was actually executed, doesn't matter the result, it always is recorded.
        // ExecutionCount uses _baseNode if available so the count is "shared" between clones
        // this may change in the future

        var nodeStatus = _handler(context.Event, this);
        ExecutionCount++;
        // we only update the node status if it was UNKNOWN, when _nodeStatus is not UNKNOWN it means Resume/Fail was already called
        _nodeStatus = (_nodeStatus == LSEventProcessStatus.UNKNOWN) ? nodeStatus : _nodeStatus;
        System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler executed with result [{nodeStatus}], _nodeStatus: {_nodeStatus}. Execution count is now {ExecutionCount}.");

        return _nodeStatus;
    }
    LSEventProcessStatus ILSEventNode.Resume(LSEventProcessContext context, params string[]? nodes) {

        if (updateNodeStatus(LSEventProcessStatus.SUCCESS, out var previousStatus)) {
            if (previousStatus != LSEventProcessStatus.WAITING) {
                System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler resume called but previous status was [{previousStatus}], expected WAITING.");
                return _nodeStatus; // resume only works if previous status was WAITING, the original context.Process caller will probably handle this
            }
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler was resumed from [{previousStatus}] to [{LSEventProcessStatus.SUCCESS}].");
            // we have to make sure we are continuing the context process.
            return context.Process();
        }
        System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler resume had no effect, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");
        // if resume had no effect, return current status
        return _nodeStatus;
    }
    LSEventProcessStatus ILSEventNode.Fail(LSEventProcessContext context, params string[]? nodes) {
        if (updateNodeStatus(LSEventProcessStatus.FAILURE, out var previousStatus)) {
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler was failed from [{previousStatus}] to [{LSEventProcessStatus.FAILURE}].");
            return context.Process();
        }
        System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler fail had no effect, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");

        return _nodeStatus;
    }
    void ILSEventNode.Cancel(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventHandlerNode] Cancelling node {NodeID}.");
        _nodeStatus = LSEventProcessStatus.CANCELLED;
    }

    public ILSEventNode Clone() {
        return new LSEventHandlerNode(NodeID, _handler, Order, Priority, this, Conditions);
    }

    public static LSEventHandlerNode Create(string nodeID, LSEventHandler handler,
                      int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventHandlerNode(nodeID, handler, order, priority, null, conditions);
    }
}
