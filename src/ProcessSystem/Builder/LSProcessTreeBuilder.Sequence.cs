using LSUtils.Logging;

namespace LSUtils.ProcessSystem;

public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Creates or navigates to a sequence node in the current context with support for nested hierarchy construction.
    /// </summary>
    /// <param name="nodeID">Unique identifier for the sequence node within the current context.</param>
    /// <param name="builderAction">Optional delegate for building nested children within this sequence. If provided, the sequence is built completely and the builder stays at the parent level.</param>
    /// <param name="priority">Processing priority level for this sequence node (default: NORMAL).</param>
    /// <param name="conditions">Optional array of conditions that must be met before this sequence processes.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSException">Thrown when attempting to override a non-sequence node with the same ID.</exception>
    /// <remarks>
    /// <para><strong>Sequence Node Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Sequential Processing</strong>: Children are processed in priority/order sequence until one fails</description></item>
    /// <item><description><strong>Early Termination</strong>: Processing stops at the first child that returns FAILURE</description></item>
    /// <item><description><strong>Success Condition</strong>: All children must succeed for the sequence to succeed</description></item>
    /// <item><description><strong>AND Logic</strong>: Implements logical AND behavior across child nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Node Creation and Replacement:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>New Node Creation</strong>: Creates a new sequence node if none exists with the given ID</description></item>
    /// <item><description><strong>Existing Node Navigation</strong>: Navigates to existing sequence node if it exists</description></item>
    /// <item><description><strong>Type Safety</strong>: Throws exception if attempting to replace non-sequence node</description></item>
    /// <item><description><strong>Order Preservation</strong>: Maintains original order when replacing existing nodes</description></item>
    /// </list>
    /// 
    /// <para><strong>Navigation Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Creation</strong>: If no root exists, this sequence becomes the root node</description></item>
    /// <item><description><strong>Sub-Context Building</strong>: When subContextBuilder is provided, stays at parent level after building</description></item>
    /// <item><description><strong>Direct Navigation</strong>: When no subContextBuilder provided, navigates into the sequence for further operations</description></item>
    /// <item><description><strong>Sibling Support</strong>: Enables creation of sibling nodes when using sub-context pattern</description></item>
    /// </list>
    /// </remarks>
    public LSProcessTreeBuilder Sequence(string nodeID,
            LSProcessBuilderAction? builderAction = null,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            params LSProcessNodeCondition?[] conditions) {
        if (_rootNode == null) {
            LSLogger.Singleton.Warning($"Root not found [{nodeID}].",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("method", nameof(Sequence))
                });
            return this;
        }
        var order = _rootNode.GetChildren().Length;

        if (getChild(nodeID, out ILSProcessNode? existingNode) == false || existingNode == null) { // no child with same nodeID existing, creating new node
            var newSequenceNode = new LSProcessNodeSequence(nodeID, order, priority, updatePolicy, conditions);
            _rootNode.AddChild(newSequenceNode);
            // builder action is invoked with a new builder context at the new nodes
            // IGNORE_BUILDER are not applied to new nodes
            builderAction?.Invoke(new LSProcessTreeBuilder(newSequenceNode));
            LSLogger.Singleton.Debug($"New sequence node [{nodeID}] created.",
                source: (ClassName, null),
                properties: new (string, object)[] {
                    ("nodeID", nodeID),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("order", order),
                    ("priority", priority.ToString()),
                    ("updatePolicy", updatePolicy.ToString()),
                    ("conditions", conditions.Length.ToString()),
                    ("method", nameof(Sequence))
                });
            return this;
        }
        // existing node found, apply update policy
        var policyOrder = existingNode.Order; // preserve order of existing node
        var policyPriority = updatePolicy.HasFlag(NodeUpdatePolicy.OVERRIDE_PRIORITY) ? priority : existingNode.Priority;
        var policyConditions = LSProcessHelpers.UpdateConditions(updatePolicy, existingNode.Conditions, conditions);
        var policyChildren = System.Array.Empty<ILSProcessNode>();
        var policyUpdatePolicy = (existingNode.UpdatePolicy | updatePolicy) & (NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.IGNORE_BUILDER);

        if (existingNode is not LSProcessNodeSequence sequenceNode) { // is not a sequence node
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
                            ("method", nameof(Sequence))
                        });

                    // the existing node is read-only or not replacing, cannot be modified and since it's not the same type, should not run builder actions
                    return this;
                }
                // default behaviour when replacing a non-sequence layer node will not preserve children
            }
        } else {
            // sequence node found, preserve existing children if not replacing layer or is read-only
            if (updatePolicy.HasFlag(NodeUpdatePolicy.REPLACE_LAYER) == false || // not replacing the layer
                sequenceNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) { // existingLayerNode is read-only

                if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) {  // existing node allows builder updates
                    builderAction?.Invoke(new LSProcessTreeBuilder(sequenceNode));
                }
                // the existing node is read-only
                return this;

            }
            policyChildren = sequenceNode.GetChildren();
        }
        sequenceNode = new LSProcessNodeSequence(nodeID,
            order: policyOrder,
            priority: policyPriority,
            updatePolicy: policyUpdatePolicy,
            conditions: policyConditions);

        if (policyChildren.Length > 0) sequenceNode.AddChildren(policyChildren);
        _rootNode.RemoveChild(nodeID);
        _rootNode.AddChild(sequenceNode);

        // invoke builder action if update policy allows it
        if (existingNode.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_BUILDER) == false) {
            builderAction?.Invoke(new LSProcessTreeBuilder(sequenceNode));
        }

        LSLogger.Singleton.Debug($"Sequence node [{nodeID}] updated.",
            source: (ClassName, null),
            properties: new (string, object)[] {
                ("nodeID", nodeID),
                ("rootNode", _rootNode?.NodeID ?? "n/a"),
                ("order", policyOrder),
                ("priority", policyPriority.ToString()),
                ("conditions", policyConditions.Length.ToString()),
                ("updatePolicy", updatePolicy.ToString()),
                ("method", nameof(Sequence))
            });

        return this;
    }

    public LSProcessTreeBuilder Sequence(LSProcessBuilderAction builderAction,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            params LSProcessNodeCondition?[] conditions) {
        string nodeID = LSProcessManager.CreateNodeID<LSProcessNodeSequence>(_rootNode);
        return Sequence(nodeID, builderAction, updatePolicy, priority, conditions);
    }
    public LSProcessTreeBuilder Sequence<TProcess>(string nodeID,
            LSProcessBuilderAction? builderAction = null,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            params LSProcessNodeCondition?[] conditions) where TProcess : LSProcess {
        return Sequence(nodeID, builderAction, updatePolicy, priority, conditions);
    }
    public LSProcessTreeBuilder Sequence<TProcess>(
            LSProcessBuilderAction? builderAction = null,
            LSProcessPriority priority = LSProcessPriority.NORMAL,
            NodeUpdatePolicy updatePolicy = NodeUpdatePolicy.DEFAULT_LAYER,
            params LSProcessNodeCondition?[] conditions) where TProcess : LSProcess {
        string nodeID = LSProcessManager.CreateNodeID<LSProcessNodeSequence>(_rootNode);
        return Sequence(nodeID, builderAction, updatePolicy, priority, conditions);
    }

}
