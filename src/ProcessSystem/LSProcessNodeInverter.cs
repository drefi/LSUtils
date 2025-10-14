namespace LSUtils.ProcessSystem;

using System.Collections.Generic;
using LSUtils.Logging;

/// <summary>
/// Decorator node that inverts the SUCCESS/FAILURE results of its single child node.
/// <para>
/// LSProcessNodeInverter implements the Decorator Pattern to modify the execution results
/// of a single child node, converting SUCCESS to FAILURE and FAILURE to SUCCESS while
/// preserving all other status values (WAITING, CANCELLED, UNKNOWN). This enables
/// flexible conditional logic and negative conditions within process hierarchies.
/// </para>
/// <para>
/// <b>Inversion Logic:</b><br/>
/// - SUCCESS → FAILURE: Child success becomes inverter failure<br/>
/// - FAILURE → SUCCESS: Child failure becomes inverter success<br/>
/// - WAITING → WAITING: Preserved for continued async processing<br/>
/// - CANCELLED → CANCELLED: Preserved as terminal cancellation state<br/>
/// - UNKNOWN → UNKNOWN: Preserved for error handling
/// </para>
/// <para>
/// <b>Single Child Constraint:</b><br/>
/// Unlike other layer nodes, inverters can only have exactly one child node.
/// This constraint enforces the decorator pattern semantics and prevents
/// ambiguous inversion behavior with multiple children.
/// </para>
/// <para>
/// <b>Delegation Pattern:</b><br/>
/// All operations (Execute, Resume, Fail, Cancel) are delegated to the child
/// node with result inversion applied only to Execute operations. This ensures
/// proper async operation handling while maintaining inversion semantics.
/// </para>
/// </summary>
/// <example>
/// Common inversion patterns:
/// <code>
/// // Invert a validation - succeed when validation fails
/// builder.Inverter("reject-invalid-data", inv => inv
///     .Handler("validate", StrictValidationHandler));
///
/// // Negative condition logic
/// builder.Selector("fallback-strategy", sel => sel
///     .Handler("primary", PrimaryHandler)
///     .Inverter("not-busy", inv => inv
///         .Handler("check-busy", CheckSystemBusy))
///     .Handler("secondary", SecondaryHandler));
///
/// // Complex conditional with inverted subcondition
/// builder.Sequence("conditional-flow", seq => seq
///     .Handler("setup", SetupHandler)
///     .Inverter("unless-disabled", inv => inv
///         .Handler("check-disabled", CheckDisabledHandler))
///     .Handler("execute", ExecuteHandler));
/// </code>
/// </example>
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
