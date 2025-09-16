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

    protected LSEventHandlerNode(string nodeID, LSEventHandler handler,
                      LSPriority priority = LSPriority.NORMAL, LSEventHandlerNode? baseNode = null, params LSEventCondition?[] conditions) {
        _baseNode = baseNode;
        NodeID = nodeID;
        Priority = priority;
        _handler = handler;
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

    LSEventProcessStatus ILSEventNode.Process(LSEventProcessContext context) {

        // we don't need to check for cancellation here because ProcessContext already does it
        // and it won't call this method if the context is cancelled.
        // if (context.IsCancelled) return LSEventProcessStatus.CANCELLED;
        // foreach (LSEventCondition condition in Conditions.GetInvocationList()) {
        //     if (!condition(context.Event, this)) {
        //         return LSEventProcessStatus.SUCCESS; // skip if any conditions are not met
        //     }
        // }
        // increment execution count indicating the handler was actually executed, doesn't matter the result, it always is recorded.
        // ExecutionCount uses _baseNode if available so the count is "shared" between clones
        ExecutionCount++;
        var result = _handler(context.Event, this);
        var key = context.getNodeResultKey(this);
        if (!context.registerProcessStatus(key, result, out var updatedStatus)) {
            // if registration failed it means the result was was already in a final state (SUCCESS, FAILURE or CANCELLED) and cannot be updated
            // meaning it was updated by the Resume/Fail logic before we could register the result
            // in this case we return the updated status.
            return updatedStatus;
        }
        // this means the result was successfully registered and we return the result of the handler
        return result;
    }

    public ILSEventNode Clone() {
        return new LSEventHandlerNode(NodeID, _handler, Priority, this, Conditions);
    }

    public static LSEventHandlerNode Create(string nodeID, LSEventHandler handler,
                      LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        return new LSEventHandlerNode(nodeID, handler, priority, null, conditions);
    }
}
