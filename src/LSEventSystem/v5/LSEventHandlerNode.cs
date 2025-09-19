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

    LSEventProcessStatus ILSEventNode.Process(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventHandlerNode] Processing node [{NodeID}], status: [{_nodeStatus}]");
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
        System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler resume called, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");
        if (_nodeStatus != LSEventProcessStatus.WAITING && _nodeStatus != LSEventProcessStatus.UNKNOWN) {
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler cannot be resumed because its current status is [{_nodeStatus}].");
            return _nodeStatus; // can only resume if waiting or unknown
        }
        _nodeStatus = LSEventProcessStatus.SUCCESS;
        // if resume had no effect, return current status
        return _nodeStatus;
    }
    LSEventProcessStatus ILSEventNode.Fail(LSEventProcessContext context, params string[]? nodes) {
        System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler fail called, current status is [{_nodeStatus}]. nodes: {string.Join(", ", nodes ?? System.Array.Empty<string>())}");
        if (_nodeStatus != LSEventProcessStatus.WAITING && _nodeStatus != LSEventProcessStatus.UNKNOWN) {
            System.Console.WriteLine($"[LSEventHandlerNode] {NodeID} handler cannot be failed because its current status is [{_nodeStatus}].");
            return _nodeStatus; // can only fail if waiting or unknown
        }
        _nodeStatus = LSEventProcessStatus.FAILURE;
        // if resume had no effect, return current status
        return _nodeStatus;
    }
    LSEventProcessStatus ILSEventNode.Cancel(LSEventProcessContext context) {
        System.Console.WriteLine($"[LSEventHandlerNode] Cancelling node {NodeID}.");
        _nodeStatus = LSEventProcessStatus.CANCELLED;
        return _nodeStatus;
    }

    public ILSEventNode Clone() {
        return new LSEventHandlerNode(NodeID, _handler, Order, Priority, this, Conditions);
    }

    public static LSEventHandlerNode Create(string nodeID, LSEventHandler handler,
                      int order, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventHandlerNode(nodeID, handler, order, priority, null, conditions);
    }
}
