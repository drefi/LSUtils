namespace LSUtils.ProcessSystem;

using System;
using System.Linq;
using LSUtils.Logging;
public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Differently from others, a inverter node always has one child, so the builder action is required.
    /// </summary>
    /// <param name="nodeID"></param>
    /// <param name="builderAction"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Inverter(string nodeID,
            LSProcessBuilderAction builderAction,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        if (_rootNode == null) {
            LSLogger.Singleton.Error($"Root not found [{nodeID}].",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Inverter))
                });
            throw new LSException($"Root not found [{nodeID}].");
        }
        var order = _rootNode.GetChildren().Length;
        if (getChild(nodeID, out ILSProcessNode? existingNode) == false || existingNode == null) {
            // No children found create new inverter node
            var node = new LSProcessNodeInverter(nodeID, priority, order, updatePolicy, conditions);
            _rootNode.AddChild(node);
            // Build the single required child using the provided builder action
            builderAction(new LSProcessTreeBuilder(node));
            return this;
        }
        if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) {
            LSLogger.Singleton.Warning($"Node [{nodeID}] already exists and is read-only, cannot modify.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Inverter))
                });

            if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) {// Allow building children
                if (existingNode is LSProcessNodeInverter readOnlyInverter) { // only if it's an inverter node
                    builderAction(new LSProcessTreeBuilder(readOnlyInverter));
                }
            }
            return this;
        }

        ILSProcessNode? policyChild;
        int policyOrder = existingNode.Order;
        LSProcessNodeCondition?[] policyConditions = LSProcessHelpers.UpdateConditions(updatePolicy, existingNode.Conditions, conditions);
        LSProcessPriority policyPriority = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PRIORITY) ? priority : existingNode.Priority;
        if (existingNode is not LSProcessNodeInverter inverterNode) {
            // existing node is not an inverter node
            if (existingNode is ILSProcessLayerNode existingLayerNode) {
                // existing node is a layer
                if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false) {
                    //since we are not replacing the layer, cannot modify non-inverter nodes
                    LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not an inverter node, cannot modify.",
                        source: (ClassName, true),
                        properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("existingNodeType", existingNode.GetType().Name),
                        ("method", nameof(Inverter))
                        });
                    return this;
                }
                policyChild = existingLayerNode.GetChildren().FirstOrDefault();
            } else {
                // existing node is not a layer, so no child to preserve
                policyChild = null;
            }
        } else {
            // existing node is an inverter node
            policyChild = inverterNode.GetChildren().FirstOrDefault();
        }
        // updatePolicy can be applied to the inverter node, however only IGNORED_CHANGES and IGNORE_BUILDER, if applied, are kept
        // the only policy of existing nodes that matters are IGNORE_BUILDER
        // so we need to check if updatePolicy have those flags, and check if existing node have IGNORE_BUILDER
        // using bitwise operators
        NodeUpdatePolicy policyUpdatePolicy = (updatePolicy | existingNode.UpdatePolicy) & (NodeUpdatePolicy.IGNORE_BUILDER | NodeUpdatePolicy.IGNORE_CHANGES);

        var inverter = new LSProcessNodeInverter(nodeID, policyPriority, policyOrder, policyUpdatePolicy, policyConditions);
        _rootNode.RemoveChild(nodeID);
        _rootNode.AddChild(inverter);

        // Let the builder define the single child; if it doesn't, preserve the prior child
        builderAction(new LSProcessTreeBuilder(inverter));
        if (!inverter.GetChildren().Any() && policyChild != null) {
            inverter.AddChild(policyChild);
        }

        LSLogger.Singleton.Debug($"Node Inverter updated [{nodeID}]",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("nodeOrder", inverter.Order.ToString()),
                ("nodePriority", inverter.Priority.ToString()),
                ("conditions", inverter.Conditions.Length.ToString()),
                ("readOnly", inverter.ReadOnly.ToString()),
                ("updatePolicy", updatePolicy.ToString()),
                ("method", nameof(Inverter))
            });

        return this;
        /**
        // LEGACY LOGIC BEFORE UPDATE POLICY
        ILSProcessNode? existingNode = null;
        int order = _rootNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        var childFound = getChild(nodeID, out existingNode);
        // Flow debug logging
        var actionDebug = childFound ? "create" : "update";
        LSLogger.Singleton.Debug($"{ClassName}.Inverter: [{nodeID}] {actionDebug}.",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));

        if (existingNode is LSProcessNodeInverter inverterNode) {
            // update only if the node is not read-only
            if (!existingNode.ReadOnly) {
                // if fields are not provided keep the existing values
                priority ??= existingNode.Priority;
                // update values
                inverterNode.Priority = priority.Value;
                inverterNode.Conditions = LSProcessHelpers.UpdateConditions(updatePolicy, inverterNode.Conditions, conditions);
            }
            // continue to build children if builder provided even if the node is read-only
        } else {
            if (childFound) {
                //child is not an inverter node, log warning
                LSLogger.Singleton.Warning($"Node [{nodeID}] exists but is not an inverter node.",
                    source: (ClassName, null),
                    properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                        ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                        ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                        ("order", order),
                        ("priority", priority?.ToString() ?? "n/a"),
                        ("conditions", conditions.Length.ToString()),
                        ("method", nameof(Inverter))
                    });
                return this;
            }
            priority ??= LSProcessPriority.NORMAL; // default priority
            inverterNode = new LSProcessNodeInverter(nodeID, priority.Value, order, readOnly, conditions);
            if (_rootNode == null) _rootNode = inverterNode;
            else _rootNode.AddChild(inverterNode);
        }
        builder(new LSProcessTreeBuilder(inverterNode));
        // Detailed debug logging
        LSLogger.Singleton.Debug($"Node Inverter {actionDebug}",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("nodeOrder", inverterNode.Order.ToString()),
                ("nodePriority", inverterNode.Priority.ToString()),
                ("conditions", conditions.Length.ToString()),
                ("readOnly", readOnly.ToString()),
                ("overrideConditions", true),
                ("method", nameof(Inverter))
            });
        return this;
        **/
    }

    public LSProcessTreeBuilder Inverter(
        LSProcessBuilderAction builderAction,
        NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
        LSProcessPriority priority = LSProcessPriority.NORMAL,
        params LSProcessNodeCondition?[] conditions) {
        string nodeID = $"{_rootNode?.NodeID ?? "root"}-inverter-{System.Guid.NewGuid()}";
        return Inverter(nodeID, builderAction, updatePolicy, priority, conditions);
    }

    /// <summary>
    /// Creates a type-safe inverter node for strongly-typed processes.
    /// </summary>
    /// <typeparam name="TProcess">The specific process type for compile-time type safety.</typeparam>
    /// <param name="nodeID">Unique identifier for the inverter node.</param>
    /// <param name="builderAction">Action to configure the single child node.</param>
    /// <param name="priority">Execution priority (default: NORMAL).</param>
    /// <param name="overrideConditions">If true, replaces existing conditions; if false, appends to them.</param>
    /// <param name="readOnly">If true, prevents further modifications to the node.</param>
    /// <param name="conditions">Optional array of type-safe conditions for execution.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Inverter&lt;MyProcess&gt;("reject-invalid", inv => inv
    ///     .Handler&lt;MyProcess&gt;("validate", session => 
    ///         session.Process.IsValid ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE),
    ///     conditions: process => process.IsEnabled);
    /// </code>
    /// </example>
    public LSProcessTreeBuilder Inverter<TProcess>(
        string nodeID,
        LSProcessBuilderAction builderAction,
        NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
        LSProcessPriority priority = LSProcessPriority.NORMAL,
        params LSProcessNodeCondition<TProcess>?[] conditions)
        where TProcess : LSProcess {

        // Convert generic conditions to non-generic
        var convertedConditions = conditions
            .Where(c => c != null)
            .Select(c => c!.ToCondition())
            .ToArray();

        return Inverter(nodeID, builderAction, updatePolicy, priority, convertedConditions);
    }
}
