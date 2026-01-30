namespace LSUtils.ProcessSystem;

using LSUtils.Logging;
public partial class LSProcessTreeBuilder {
    /// <summary>
    /// Merges a sub-builder's constructed layer node hierarchy into the current root node.
    /// </summary>
    /// <param name="subBuilder"></param>
    /// <returns></returns>
    /// <exception cref="LSArgumentNullException"></exception>
    public LSProcessTreeBuilder Merge(LSProcessBuilderAction subBuilder) {
        if (subBuilder == null) {
            //log warning
            LSLogger.Singleton.Warning($"Cannot merge an invalid builder",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("subBuilder", "n/a"),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("method", nameof(Merge))
                });
            throw new LSArgumentNullException(nameof(subBuilder), "Provided subBuilder is null.");
        }
        return Merge(subBuilder(this).Build());
    }
    /// <summary>
    /// Merges a sub-layer node hierarchy into the current root node.
    /// </summary>
    /// <param name="subLayer">The sub-layer node hierarchy to merge into the root node.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="LSArgumentNullException">Thrown when subLayer is null.</exception>
    /// <exception cref="LSException">Thrown when no current context exists and subLayer is empty.</exception>
    /// <remarks>
    /// <para><strong>Merge Strategies:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Root Seeding</strong>: If no root context exists, subLayer becomes the root</description></item>
    /// <item><description><strong>Type-Based Merging</strong>: Layer nodes with same nodeID and the same layer type are merged recursively, handlers are replaced if the target is not read-only.</description></item>
    /// </list>
    /// </remarks>
    public LSProcessTreeBuilder Merge(ILSProcessLayerNode subLayer) {
        if (subLayer == null) {
            //log warning
            LSLogger.Singleton.Warning($"Cannot merge an invalid node",
                source: (ClassName, true),
                properties: new (string, object)[] {
                    ("subLayer", "n/a"),
                    ("rootNode", _rootNode?.NodeID ?? "n/a"),
                    ("method", nameof(Merge))
                });
            throw new LSArgumentNullException(nameof(subLayer), "Provided subLayer is null.");
        }
        LSLogger.Singleton.Debug($"{ClassName}.Merge: [{subLayer.NodeID}] into [{_rootNode?.NodeID ?? "n/a"}]",
            source: ("LSProcessSystem", null),
            properties: ("hideNodeID", true));
        if (_rootNode == null) {
            //NOTE: we don't need to merge recursive if the root is null, but maybe we need to check if the subLayer has children?
            //if (subLayer.GetChildren().Length == 0) throw new LSException("Provided sublayer invalid.");
            _rootNode = subLayer;
            LSLogger.Singleton.Debug($"Root node set to [{subLayer.NodeID}]",
                source: ("LSProcessSystem", null),
                properties: ("hideNodeID", true));
        } else if (_rootNode.NodeID == subLayer.NodeID) { // merging into root node if they have the same ID
            if (_rootNode.GetType() != subLayer.GetType()) {
                // different types log warning
                LSLogger.Singleton.Warning($"Node [{subLayer.NodeID}] is different from the root node.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                        ("nodeID", subLayer.NodeID),
                        ("subLayerType", subLayer.GetType().Name),
                        ("rootType", _rootNode.GetType().Name),
                        ("method", nameof(Merge))
                    });
                return this;
            }
            LSLogger.Singleton.Debug($"Root and subLayer have the same ID [{subLayer.NodeID}], merging contents.",
                source: ("LSProcessSystem", null),
                properties: ("hideNodeID", true));
            mergeRecursive(_rootNode, subLayer);
        } else {
            var existingLayer = _rootNode.GetChild(subLayer.NodeID) as ILSProcessLayerNode;
            if (existingLayer == null) {
                // no existing node with the same ID, just add the entire subLayer directly
                LSLogger.Singleton.Debug($"Merge: [{subLayer.NodeID}] [{_rootNode.NodeID}]",
                    source: ("LSProcessSystem", null),
                    properties: ("hideNodeID", true));
                _rootNode.AddChild(subLayer);
                return this;
            } else mergeNode(_rootNode, subLayer);
        }
        _rootNode.Reorder(0);

        return this;
    }
    /// <summary>
    /// Merges a source node into a target layer node with conflict resolution based on node types.
    /// </summary>
    /// <param name="targetLayerNode"></param>
    /// <param name="sourceNode"></param>
    private void mergeNode(ILSProcessLayerNode targetLayerNode, ILSProcessNode sourceNode) {
        var existingChild = targetLayerNode.GetChild(sourceNode.NodeID);
        if (existingChild == null) {
            targetLayerNode.AddChild(sourceNode);
            return;
        }

        // Node exists, check if both are layer nodes for recursive merging
        if (existingChild is ILSProcessLayerNode existingLayer && sourceNode is ILSProcessLayerNode subNodeLayer) {
            // Both are layer nodes - merge recursively
            mergeRecursive(existingLayer, subNodeLayer);
        } else if (existingChild.GetType() == sourceNode.GetType()) {
            // Same type but not layer nodes (e.g., both handlers)
            if (existingChild.UpdatePolicy.HasFlag(NodeUpdatePolicy.IGNORE_CHANGES)) {
                LSLogger.Singleton.Warning($"Node [{sourceNode.NodeID}] exists but is read-only, cannot replace.",
                    source: (ClassName, true),
                    properties: new (string, object)[] {
                            ("nodeID", sourceNode.NodeID),
                            ("subNodeType", sourceNode.GetType().Name),
                            ("existingType", existingChild.GetType().Name),
                            ("method", nameof(mergeNode))
                });
                return;
            }
            // Replace existing node with source node
            targetLayerNode.RemoveChild(sourceNode.NodeID);
            targetLayerNode.AddChild(sourceNode);
        } else {
            // Different types - log warning and continue
            LSLogger.Singleton.Warning($"Node [{sourceNode.NodeID}] already exists but is different type.",
                source: (ClassName, true),
                properties: new (string, object)[] {
                            ("nodeID", sourceNode.NodeID),
                            ("subNodeType", sourceNode.GetType().Name),
                            ("existingType", existingChild.GetType().Name),
                            ("method", nameof(mergeNode))
                });
        }
    }

    /// <summary>
    /// Recursively merges the contents of a source node hierarchy into a target node hierarchy.
    /// In merge targetNode is always treated as readOnly so sourceNode cannot change priority or conditions.
    /// To make this work otherwise, sourceNode would have to be cast as a concrete node or the change the interface.
    /// </summary>
    /// <param name="targetNode">The target layer node that will receive merged content.</param>
    /// <param name="sourceNode">The source layer node whose content will be merged into the target.</param>
    private void mergeRecursive(ILSProcessLayerNode targetNode, ILSProcessLayerNode sourceNode) {
        // Iterate through all children of the source node
        foreach (var sourceChild in sourceNode.GetChildren()) {
            mergeNode(targetNode, sourceChild);
        }
    }
}
