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

    public bool ReadOnly { get; internal set; } = false;

    protected LSProcessNodeInverter(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL, int order = 0, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        NodeID = nodeID;
        Priority = priority;
        Order = order;
        Conditions = LSProcessHelpers.UpdateConditions(true, Conditions, conditions);
        ReadOnly = readOnly;
    }

    // inverter should not be able to change child after creation
    public void AddChild(ILSProcessNode child) {
        if (this._childNode != null) {
            throw new LSException("Can't add more than a single child to LSProcessNodeInverter!");
        }

        _childNode = child;
    }

    public LSProcessResultStatus Execute(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Execute [{NodeID}]",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));

        if (_childNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Inverter does not have child.",
                source: (ClassName, null),
                processId: session.Process.ID,
                properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("method", nameof(Execute))
                });
            return LSProcessResultStatus.UNKNOWN;
        }

        var result = _childNode.Execute(session);
        LSLogger.Singleton.Debug($"Inverter Node Execute.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode.NodeID),
                ("result", result.ToString()),
                ("method", nameof(Execute))
            });
        return result switch {
            LSProcessResultStatus.SUCCESS => LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.FAILURE => LSProcessResultStatus.SUCCESS,
            _ => result,
        };
    }

    public LSProcessResultStatus Fail(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Fail [{NodeID}]",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));

        if (_childNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Inverter does not have child.",
                source: (ClassName, null),
                processId: session.Process.ID,
                properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                    ("method", nameof(Fail))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        LSLogger.Singleton.Debug($"Inverter Node Fail.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode.NodeID),
                ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                ("method", nameof(Fail))
            });
        return _childNode.Fail(session, nodes);
    }

    public LSProcessResultStatus Resume(LSProcessSession session, params string[]? nodes) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Resume [{NodeID}]",
              source: ("LSProcessSystem", null),
              processId: session.Process.ID,
              properties: ("hideNodeID", true));
        if (_childNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Inverter does not have child.",
                source: (ClassName, null),
                processId: session.Process.ID, properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                    ("method", nameof(Resume))
                });
            return LSProcessResultStatus.UNKNOWN;
        }
        // Debug log with details
        LSLogger.Singleton.Debug($"Inverter Node Resume.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode.NodeID),
                ("nodes", nodes != null ? string.Join(",", nodes) : "null"),
                ("method", nameof(Resume))
            });
        return _childNode.Resume(session, nodes);
    }
    public LSProcessResultStatus Cancel(LSProcessSession session) {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Cancel [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true));
        if (_childNode == null) {
            //log warning
            LSLogger.Singleton.Warning($"Inverter does not have child.",
                source: (ClassName, null),
                processId: session.Process.ID,
                properties: new (string, object)[] {
                    ("nodeID", NodeID),
                    ("method", nameof(Cancel))
                });
            return LSProcessResultStatus.UNKNOWN;
        }

        LSLogger.Singleton.Debug($"Inverter Node Cancel.",
              source: (ClassName, null),
              processId: session.Process.ID,
              properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode.NodeID),
                ("method", nameof(Cancel))
            });
        return _childNode.Cancel(session);
    }
    public ILSProcessLayerNode Clone() {
        // Flow debug logging
        LSLogger.Singleton.Debug($"{ClassName}.Clone [{NodeID}]",
              source: ("LSProcessSystem", null),
              properties: ("hideNodeID", true)); // No specific process context for node cloning

        var clone = new LSProcessNodeInverter(NodeID, Priority, Order, ReadOnly, Conditions);
        if (_childNode != null) clone.AddChild(_childNode.Clone());
        // Debug log with details
        LSLogger.Singleton.Debug($"Inverter node cloned.",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", NodeID),
                ("nodeChild", _childNode?.NodeID ?? "null"),
                ("method", nameof(Clone))
            });
        return clone;
    }
    ILSProcessNode ILSProcessNode.Clone() => (ILSProcessLayerNode)Clone();

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

    public static LSProcessNodeInverter Create(string nodeID, LSProcessPriority priority = LSProcessPriority.NORMAL, int order = 0, bool readOnly = false, params LSProcessNodeCondition?[] conditions) {
        var node = new LSProcessNodeInverter(nodeID, priority, order, readOnly, conditions);
        return node;
    }
}
