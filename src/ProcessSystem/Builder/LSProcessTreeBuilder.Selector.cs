using System.Linq;
using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Creates or navigates to a selector node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the selector node within the current context.</param>
    /// <param name="builderAction">Optional delegate for building nested children within this selector. If provided, the selector is built completely and the builder stays at the parent level.</param>
    /// <param name="updatePolicy">Policy flags controlling how existing nodes are handled during creation or update.</param>
    /// <param name="priority">Processing priority level for this selector node (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met before this selector processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <remarks>
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new selector node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Navigation</strong>: Navigates to existing selector node if it exists</description></item>
    /// <item><description><strong>Read-Only Handling</strong>: Skips modification if existing node is read-only, but still allows building children if existing node is also a Selector</description></item>
    /// <item><description><strong>Node Replacement</strong>: Replaces existing node based on updatePolicy flags, preserving order. Children are lost.</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order when replacing existing nodes</description></item>
    /// </list>
    /// </remarks>
    public LSProcessTreeBuilder Selector(string nodeID,
            LSProcessBuilderAction? builderAction = null,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        if (_rootNode == null) {
            LSLogger.Singleton.Warning($"Root not found [{nodeID}].",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Selector))
                });
            return this;
        }
        var order = _rootNode.GetChildren().Length;

        if (getChild(nodeID, out ILSProcessNode? existingNode) == false || existingNode == null) { // no child with same nodeID existing, creating new node
            var newSelectorNode = new LSProcessNodeSelector(nodeID, order, priority, updatePolicy, conditions);
            _rootNode.AddChild(newSelectorNode);
            // builder action is invoked with a new builder context at the new nodes
            // IGNORE_BUILDER are not applied to new nodes
            builderAction?.Invoke(new LSProcessTreeBuilder(newSelectorNode));
            LSLogger.Singleton.Debug($"New selector node [{nodeID}] created.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("order", order),
                    ("priority", priority.ToString()),
                    ("updatePolicy", updatePolicy.ToString()),
                    ("conditions", conditions.Length.ToString()),
                    ("method", nameof(Selector))
                });
            return this;
        }
        // existing node found, apply update policy
        var policyOrder = existingNode.Order; // preserve order of existing node
        var policyPriority = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PRIORITY) ? priority : existingNode.Priority;
        var policyConditions = LSProcessHelpers.UpdateConditions(updatePolicy, existingNode.Conditions, conditions);
        var policyChildren = System.Array.Empty<ILSProcessNode>();
        var policyUpdatePolicy = (existingNode.UpdatePolicy | updatePolicy) & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);

        if (existingNode is not LSProcessNodeSelector selectorNode) { // is not a selector node
            if (existingNode is ILSProcessLayerNode existingLayerNode) { // but is a layer node
                if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // not replacing the layer
                    existingLayerNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existingLayerNode is read-only
                    LSLogger.Singleton.Warning($"Node [{nodeID}] exists but update policy does not allow replacing the layer.",
                        source: (ClassName, true),
                        properties: new (string, object)[] {
                            ("nodeID", nodeID),
                            ("rootNode", _rootNode?.NodeID ?? "n/a"),
                            ("existingLayerNodeType", existingLayerNode.GetType().Name),
                            ("existingLayerNodeOrder", existingLayerNode?.Order.ToString() ?? "n/a"),
                            ("existingLayerNodePriority", existingLayerNode?.Priority.ToString() ?? "n/a"),
                            ("existingLayerNodeConditions", existingLayerNode?.Conditions.Length.ToString() ?? "n/a"),
                            ("existingLayerNodeUpdatePolicy", existingLayerNode?.UpdatePolicy.ToString() ?? "n/a"),
                            ("order", order),
                            ("priority", priority.ToString()),
                            ("updatePolicy", updatePolicy.ToString()),
                            ("conditions", conditions != null ? conditions.Length.ToString() : "n/a"),
                            ("method", nameof(Selector))
                        });

                    // the existing node is read-only or not replacing, cannot be modified and since it's not the same type, should not run builder actions
                    return this;
                }
                // default behaviour when replacing a non-sequence layer node will not preserve children
            }
        } else {
            // selector node found, preserve existing children if not replacing layer
            if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // not replacing the layer
                selectorNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existingLayerNode is read-only

                if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) {  // existing node allows builder updates
                    builderAction?.Invoke(new LSProcessTreeBuilder(selectorNode));
                }
                return this;
            }

            policyChildren = selectorNode.GetChildren();
        }
        selectorNode = new LSProcessNodeSelector(nodeID,
            order: policyOrder,
            priority: policyPriority,
            updatePolicy: policyUpdatePolicy,
            conditions: policyConditions);

        if (policyChildren.Length > 0) selectorNode.AddChildren(policyChildren);
        _rootNode.RemoveChild(nodeID);
        _rootNode.AddChild(selectorNode);

        if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false)
            builderAction?.Invoke(new LSProcessTreeBuilder(selectorNode));

        LSLogger.Singleton.Debug($"Selector node [{nodeID}] updated.",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", policyOrder),
                ("priority", policyPriority.ToString()),
                ("conditions", policyConditions.Length.ToString()),
                ("updatePolicy", updatePolicy.ToString()),
                ("method", nameof(Selector))
            });

        return this;
    }

    /// <summary>
    /// Creates a selector node with an auto-generated unique identifier.
    /// </summary>
    /// <param name="builderAction"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Selector(LSProcessBuilderAction builderAction,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        string nodeID = $"{_rootNode?.NodeID ?? "root"}-selector-{System.Guid.NewGuid()}";
        return Selector(nodeID, builderAction, updatePolicy, priority, conditions);
    }

    /// <summary>
    /// Creates a type-safe selector node for strongly-typed processes.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    /// <param name="nodeID"></param>
    /// <param name="builderAction"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Selector<TProcess>(
            string nodeID,
            LSProcessBuilderAction builderAction,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions) where TProcess : LSProcess {

        // Convert generic conditions to non-generic
        var convertedConditions = conditions
            .Where(c => c != null)
            .Select(c => c!.ToCondition())
            .ToArray();

        return Selector(nodeID,
            builderAction,
            updatePolicy,
            priority,
            convertedConditions);
    }

    /// <summary>
    /// Creates a type-safe selector node with an auto-generated unique identifier.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    /// <param name="builderAction"></param>
    /// <param name="updatePolicy"></param>
    /// <param name="priority"></param>
    /// <param name="conditions"></param>
    /// <returns></returns>
    public LSProcessTreeBuilder Selector<TProcess>(
            LSProcessBuilderAction builderAction,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition<TProcess>?[] conditions) where TProcess : LSProcess {

        string nodeID = LSProcessManager.CreateNodeID<LSProcessNodeSelector>(_rootNode);
        return Selector(nodeID,
            builderAction,
            updatePolicy,
            priority,
            conditions);
    }
}
