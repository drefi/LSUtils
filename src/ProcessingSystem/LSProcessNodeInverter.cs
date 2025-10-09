namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using LSUtils.Logging;
public class LSProcessNodeInverter : ILSProcessLayerNode {
    public const string ClassName = nameof(LSProcessNodeInverter);
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

    public LSProcessResultStatus Cancel(LSProcessSession session) {
        LSLogger.Singleton.Debug($"Inverter Node Cancel.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode?.NodeID ?? "null"),
                ("method", nameof(Cancel))
            });
        if (_childNode == null) return LSProcessResultStatus.UNKNOWN;
        return _childNode.Cancel(session);
    }

    public ILSProcessLayerNode Clone() {
        var clone = new LSProcessNodeInverter(NodeID, Priority, Order, Conditions);
        if (_childNode != null) {
            clone.AddChild(_childNode.Clone());
        }
        return clone;
    }

    public LSProcessResultStatus Execute(LSProcessSession session) {
        if (_childNode == null) {
            throw new LSException("LSProcessNodeInverter must have a child node!");
        }
        LSLogger.Singleton.Debug($"Inverter Node Execute.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode.NodeID),
                ("method", nameof(Execute))
            });

        var result = _childNode.Execute(session);
        return result switch {
            LSProcessResultStatus.SUCCESS => LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.FAILURE => LSProcessResultStatus.SUCCESS,
            _ => result,
        };
    }

    public LSProcessResultStatus Fail(LSProcessSession session, params string[]? nodes) {
        LSLogger.Singleton.Debug($"Inverter Node Fail.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode?.NodeID ?? "null"),
                ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                ("method", nameof(Fail))
            });
        if (_childNode == null) return LSProcessResultStatus.UNKNOWN;
        return _childNode.Fail(session, nodes);
    }

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

    public LSProcessResultStatus Resume(LSProcessSession session, params string[]? nodes) {
        LSLogger.Singleton.Debug($"Inverter Node Resume.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode?.NodeID ?? "null"),
                ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                ("method", nameof(Resume))
            });
        if (_childNode == null) return LSProcessResultStatus.UNKNOWN;
        return _childNode.Resume(session, nodes);
    }

    ILSProcessNode ILSProcessNode.Clone() {
        return Clone();
    }
    public static LSProcessNodeInverter Create(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL, int order = 0, params LSProcessNodeCondition?[] conditions) {
        var node = new LSProcessNodeInverter(nodeID, priority, order, conditions);
        return node;
    }
}
