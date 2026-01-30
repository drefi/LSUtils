using System;
using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Creates a handler node in the current layer node for executing event processing logic.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the handler node within the current layer.</param>
    /// <param name="handler">Handler delegate to execute when this node processes.</param>
    /// <param name="priority">Processing priority level (default: NORMAL).</param>
    /// <param name="updatePolicy">Policy for updating existing nodes with the same ID.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <remarks>
    /// <para><strong>Flag Independence:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>OVERRIDE_HANDLER</strong>: Controls whether the handler delegate is replaced. When omitted, existing handler delegate is preserved.</description></item>
    /// <item><description><strong>OVERRIDE_PRIORITY</strong>: Controls whether the priority is updated. When omitted, existing priority is preserved.</description></item>
    /// <item><description><strong>OVERRIDE_CONDITIONS</strong>: Controls whether conditions are updated/merged. When omitted, existing conditions are preserved.</description></item>
    /// <item><description><strong>READONLY</strong>: Applied only to new nodes; existing read-only nodes cannot be modified regardless of other flags.</description></item>
    /// </list>
    /// <para><strong>Common Scenarios:</strong></para>
    /// <list type="bullet">
    /// <item><description>
    /// <strong>Replace entire handler:</strong> Use <c>OVERRIDE_HANDLER | OVERRIDE_PRIORITY | OVERRIDE_CONDITIONS</c><br/>
    /// Example: <c>Handler("task", NewHandler, OVERRIDE_HANDLER | OVERRIDE_PRIORITY | OVERRIDE_CONDITIONS, HIGH, cond1, cond2)</c>
    /// </description></item>
    /// <item><description>
    /// <strong>Update only priority on existing handler:</strong> Use <c>OVERRIDE_PRIORITY</c> alone<br/>
    /// Example: <c>Handler("task", _, OVERRIDE_PRIORITY, HIGH)</c> — keeps existing handler delegate and conditions
    /// </description></item>
    /// <item><description>
    /// <strong>Update conditions without changing handler:</strong> Use <c>OVERRIDE_CONDITIONS</c> alone<br/>
    /// Example: <c>Handler("task", _, OVERRIDE_CONDITIONS, _, newCond)</c> — keeps existing handler and priority
    /// </description></item>
    /// <item><description>
    /// <strong>Default behavior (most common):</strong> Use <c>DEFAULT_HANDLER</c><br/>
    /// Replaces handler and priority; conditions are merged unless <c>OVERRIDE_CONDITIONS</c> is specified.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// 
    /// <param name="priority">Processing priority level (default: NORMAL). Only applied if OVERRIDE_PRIORITY is set or node is new.</param>
    /// <param name="conditions">Optional array of conditions that must be met for execution. Only applied if OVERRIDE_CONDITIONS is set or node is new.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when no layer context exists.</exception>
    /// <exception cref="LSException">Thrown when attempting to modify a read-only node (must use RemoveChild first).</exception>
    public LSProcessTreeBuilder Handler(string nodeID,
            LSProcessHandler handler,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_HANDLER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {

        if (_rootNode == null) {
            LSLogger.Singleton.Error($"Cannot add handler node [{nodeID}] because no root node exists.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Handler))
                });
            throw new LSException("Cannot add handler node because no layer context exists.");
        }

        var order = _rootNode.GetChildren().Length;
        if (getChild(nodeID, out var existingNode) == false || existingNode == null) {
            // Create new handler node, except for READONLY update policies does not matter
            var node = new LSProcessNodeHandler(nodeID, handler, order, priority, null, updatePolicy, conditions);
            _rootNode.AddChild(node);
            return this;
        }
        if ((existingNode.UpdatePolicy & NodeUpdatePolicy.IGNORE_CHANGES) != 0) { // existing node is read-only
            LSLogger.Singleton.Warning($"Node [{nodeID}] already exists and is read-only, cannot modify.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                    ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                    ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                    ("order", order.ToString()),
                    ("priority", priority.ToString()),
                    ("conditions", conditions.Length.ToString()),
                    ("method", nameof(Handler))
                });
            return this;
        }
        LSProcessHandler? policyHandler;
        if (existingNode is not LSProcessNodeHandler nodeHandler) {

            if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // unless replacing the existing node, cannot modify non-handler nodes
                existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existing node is read-only
                LSLogger.Singleton.Warning($"Node [{nodeID}] already exists but is not a NodeHandler, cannot modify.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                        ("nodeID", nodeID),
                        ("rootNode", _rootNode?.NodeID ?? "n/a"),
                        ("existingNode", existingNode?.GetType().Name ?? "n/a"),
                        ("nodeOrder", existingNode?.Order.ToString() ?? "n/a"),
                        ("nodePriority", existingNode?.Priority.ToString() ?? "n/a"),
                        ("order", existingNode?.Order.ToString() ?? "n/a"),
                        ("priority", priority.ToString()),
                        ("conditions", conditions.Length.ToString()),
                        ("method", nameof(Handler))
                    });
                return this;
            }
            // if replacing a non-handler node, can only use the provided handler
            policyHandler = handler;
        } else {
            // existing node is a handler, determine whether to override or preserve existing handler
            policyHandler = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_HANDLER) ? handler : nodeHandler.Handler;
        }
        var policyPriority = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PRIORITY) ? priority : existingNode.Priority;
        var policyCondition = LSProcessHelpers.UpdateConditions(updatePolicy, existingNode.Conditions, conditions);
        int policyOrder = existingNode.Order; // ordering can only be modified at layer level with REORDER_CHILDREN flag

        _rootNode.RemoveChild(nodeID);
        // updatePolicy is trick, should the new updatePolicy be applied or a combination of existing and new?
        var updatedNode = new LSProcessNodeHandler(nodeID, policyHandler, policyOrder, policyPriority, null, updatePolicy, policyCondition);
        _rootNode.AddChild(updatedNode);

        LSLogger.Singleton.Warning($"Handler Node updated [{nodeID}].",
            source: (ClassName, true),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", updatedNode.Order.ToString()),
                ("priority", updatedNode.Priority.ToString()),
                ("updatePolicy", updatedNode.UpdatePolicy.ToString()),
                ("conditions", updatedNode.Conditions.Length.ToString()),
                ("method", nameof(Handler))
            });

        return this;
    }

    /// <summary>
    /// Generic overload of Handler that infers the process type.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    /// <param name="nodeID"></param>
    /// <param name="handler"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Handler<TProcess>(string nodeID,
            LSProcessHandler<TProcess> handler,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_HANDLER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions) where TProcess : LSProcess {
        return Handler(nodeID, handler.ToHandler(), updatePolicy, priority, conditions.ToCondition());
    }

    /// <summary>
    /// Overload of Handler that auto-generates a unique node ID.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    /// <param name="handler"></param>
    /// <param name="nodeID"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Handler<TProcess>(LSProcessHandler<TProcess> handler,
            out string nodeID,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_HANDLER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions) where TProcess : LSProcess {
        nodeID = $"handler-{System.Guid.NewGuid()}";
        return Handler(nodeID, handler.ToHandler(), updatePolicy, priority, conditions.ToCondition());
    }

    /// <summary>
    /// Overload of Handler that omit nodeID.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    /// <param name="handler"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Handler<TProcess>(LSProcessHandler<TProcess> handler, NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority priority = LSProcessPriority.NORMAL, params LSProcessNodeCondition<TProcess>?[] conditions) where TProcess : LSProcess {
        // use overload that generates a new nodeID with (out _)
        return Handler<TProcess>(handler, out _, updatePolicy, priority, conditions);
    }
}
