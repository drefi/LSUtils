namespace LSUtils.Processing;

public class LSProcessNodeInverter : ILSProcessLayerNode {
    public string NodeID { get; }
    protected ILSProcessNode? _childNode = null;

    public LSProcessPriority Priority { get; internal set; }

    public LSProcessNodeCondition? Conditions { get; internal set; }

    int ILSProcessNode.ExecutionCount => throw new System.NotImplementedException("ExecutionCount is tracked only in handler node.");

    public int Order { get; internal set; }

    protected LSProcessNodeInverter(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL, int order = 0, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeID;
        Priority = priority;
        Order = order;
        Conditions = LSProcessConditions.UpdateConditions(true, Conditions, conditions);
    }

    // inverter should not be able to change child after creation
    public void AddChild(ILSProcessNode child) {
        if (this._childNode != null) {
            throw new LSException("Can't add more than a single child to LSProcessNodeInverter!");
        }

        _childNode = child;
    }

    public LSProcessResultStatus Cancel(LSProcessSession context) => _childNode?.Cancel(context) ?? throw new LSException("No child node to cancel.");

    public ILSProcessLayerNode Clone() {
        var clone = new LSProcessNodeInverter(NodeID, Priority, Order, Conditions);
        if (_childNode != null) {
            clone.AddChild(_childNode.Clone());
        }
        return clone;
    }

    public LSProcessResultStatus Execute(LSProcessSession context) {
        if (_childNode == null) {
            throw new LSException("LSProcessNodeInverter must have a child node!");
        }

        var result = _childNode.Execute(context);
        return result switch {
            LSProcessResultStatus.SUCCESS => LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.FAILURE => LSProcessResultStatus.SUCCESS,
            _ => result,
        };
    }

    public LSProcessResultStatus Fail(LSProcessSession context, params string[]? nodes) => _childNode?.Fail(context, nodes) ?? throw new LSException("No child node to fail.");

    public ILSProcessNode? GetChild(string nodeID) {
        if (_childNode != null && _childNode.NodeID == nodeID) {
            return _childNode;
        }
        return null;
    }

    public ILSProcessNode[] GetChildren() {
        if (_childNode != null) {
            return new ILSProcessNode[] { _childNode };
        }
        return System.Array.Empty<ILSProcessNode>();
    }

    public LSProcessResultStatus GetNodeStatus() {
        if (_childNode == null) return LSProcessResultStatus.FAILURE;

        return _childNode.GetNodeStatus() switch {
            LSProcessResultStatus.SUCCESS => LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.FAILURE => LSProcessResultStatus.SUCCESS,
            var status => status,
        };
    }

    public bool HasChild(string nodeID) {
        if (_childNode == null) return false;
        return _childNode.NodeID == nodeID;
    }

    public bool RemoveChild(string nodeID) {
        if (_childNode != null && _childNode.NodeID == nodeID) {
            _childNode = null;
            return true;
        }
        return false;
    }

    public LSProcessResultStatus Resume(LSProcessSession context, params string[]? nodes) => _childNode?.Resume(context, nodes) ?? throw new LSException("No child node to resume.");

    ILSProcessNode ILSProcessNode.Clone() {
        return Clone();
    }
    public static LSProcessNodeInverter Create(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL, int order = 0, params LSProcessNodeCondition?[] conditions) {
        var node = new LSProcessNodeInverter(nodeID, priority, order, conditions);
        return node;
    }
}
