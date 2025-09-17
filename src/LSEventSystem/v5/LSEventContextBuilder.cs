using System.Collections.Generic;

namespace LSUtils.EventSystem;

public class LSEventContextBuilder {
    private ILSEventLayerNode? _currentNode;
    private ILSEventLayerNode? _rootNode; // Tracks the root node to return on Build()

    // Constructor for global mode
    internal LSEventContextBuilder() {
        //when creating a "global" context we start with no root context
    }

    // Constructor for event mode
    internal LSEventContextBuilder(ILSEventLayerNode rootNode) {
        _currentNode = _rootNode = rootNode.Clone();
    }

    /// <summary>
    /// Create a handler node in the current context.
    /// If no layer node is active, an exception will be thrown.
    /// If a node with the same ID already exists in the current context and override is set to true, it will replace the existing node.
    /// If the removed node is not a handler node, an exception will be thrown.
    /// </summary>
    /// <param name="nodeID">The ID of the node to remove.</param>
    /// <param name="handler">The event handler to associate with the node.</param>
    /// <param name="priority">The priority of the event handler.</param>
    /// <param name="conditions">The conditions under which the event handler will be executed.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Execute(string nodeID, LSEventHandler handler, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if (_currentNode == null) throw new LSException("No current context to add the handler. Make sure to start with a layer node (Sequence, Selector, Parallel).");
        // Check if a node with the same ID already exists
        if (tryGetContextChild(nodeID, out var existingNode)) {
            // Node with same ID already exists
            // If the existing node is not a handler node, we cannot override it with a handler node
            if (existingNode is not LSEventHandlerNode) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a handler node.");
            // remove the previous handler node
            _currentNode.RemoveChild(nodeID);
        }
        int order = _currentNode.GetChildren().Length; // Order is based on the number of existing children
        LSEventHandlerNode handlerNode = LSEventHandlerNode.Create(nodeID, handler, order, priority, conditions);
        _currentNode.AddChild(handlerNode);

        return this;
    }

    public LSEventContextBuilder RemoveChild(string nodeID) {
        if (_currentNode == null) throw new LSException("No current context to remove the node from. Make sure to start with a layer node (Sequence, Selector, Parallel).");
        // Removing a non-existent node should be a no-op (no exception)
        _currentNode.RemoveChild(nodeID);

        return this;
    }
    public delegate LSEventContextBuilder SubContextBuilder(LSEventContextBuilder subBuilder);
    /// <summary>
    /// Create or navigate to a sequence node in the current context.
    /// When creating a new sequence node, if a node with the same ID already exists it will be replaced.
    /// </summary>
    /// <param name="nodeID">The ID of the sequence node.</param>
    /// <param name="priority">The priority of the sequence node.</param>
    /// <param name="conditions">The conditions for the sequence node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Sequence(string nodeID, SubContextBuilder? subContextBuilder = null, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSequenceNode sequenceNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not sequence
            sequenceNode = LSEventSequenceNode.Create(nodeID, order, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSequenceNode we should throw an exception.
            //if existingNode is not null here is means is not a sequence node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a sequence node.");
            _currentNode?.RemoveChild(nodeID); // remove existing node if exists
        }
        // when we reach this point, sequenceNode is either a new node or the existing sequence node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(sequenceNode)).Build() ?? sequenceNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Create or navigate to a selector node in the current context.
    /// When creating a new selector node, if a node with the same ID already exists it will be replaced.
    /// </summary>
    /// <param name="nodeID">The ID of the selector node.</param>
    /// <param name="priority">The priority of the selector node.</param>
    /// <param name="conditions">The conditions for the selector node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Selector(string nodeID, SubContextBuilder? subContextBuilder = null, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventSelectorNode selectorNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not selector
            selectorNode = LSEventSelectorNode.Create(nodeID, order, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventSelectorNode we should throw an exception.
            //if existingNode is not null here is means is not a selector node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a selector node.");
            _currentNode?.RemoveChild(nodeID);
        }
        // when we reach this point, selectorNode is either a new node or the existing selector node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(selectorNode)).Build() ?? selectorNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }

    /// <summary>
    /// Create or navigate to a parallel node in the current context.
    /// </summary>
    /// <param name="nodeID">The ID of the parallel node.</param>
    /// <param name="numRequiredToSucceed">The number of child nodes that must succeed.</param>
    /// <param name="priority">The priority of the parallel node.</param>
    /// <param name="conditions">The conditions for the parallel node process.</param>
    /// <returns> The current instance of the context builder.</returns>
    /// <exception cref="LSException"></exception>
    public LSEventContextBuilder Parallel(string nodeID, SubContextBuilder? subContextBuilder = null, int? numRequiredToSucceed = 0, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        ILSEventNode? existingNode = null;
        var parentBefore = _currentNode;
        int order = _currentNode?.GetChildren().Length ?? 0; // Order is based on the number of existing children, or 0 if root
        if (_currentNode == null || !tryGetContextChild(nodeID, out existingNode) || existingNode is not LSEventParallelNode parallelNode) {
            // we keep the original order of the existing node
            order = existingNode?.Order ?? order;
            // no current node exists, so we are creating the root node or node does not exist or node exists but is not parallel
            parallelNode = LSEventParallelNode.Create(nodeID, order, numRequiredToSucceed == null ? 0 : numRequiredToSucceed.Value, priority, conditions);
            // If we are overriding the node, we need to remove the existing one, but if the existingNode is not LSEventParallelNode we should throw an exception.
            //if existingNode is not null here is means is not a parallel node
            if (existingNode != null) throw new LSException($"Node with ID '{nodeID}' already exists in the current context and is not a parallel node.");
            _currentNode?.RemoveChild(nodeID);
        } else {
            // node exists and is a parallel node, update numRequiredToSucceed if provided
            if (numRequiredToSucceed != null) parallelNode.NumRequiredToSucceed = numRequiredToSucceed.Value;
        }
        // when we reach this point, parallelNode is either a new node or the existing parallel node
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(parallelNode)).Build() ?? parallelNode;
        // we only add the node if it was newly created and we had a current context
        if (parentBefore != null && existingNode == null) parentBefore.AddChild(node);
        // If there was no root yet, this node becomes the root
        if (_rootNode == null) _rootNode = node;
        // Navigation: if we created a root (no parent) or no sub-builder provided, navigate into the node; otherwise keep the parent to allow siblings
        _currentNode = (parentBefore == null || subContextBuilder == null) ? node : parentBefore;

        return this;
    }
    /// <summary>
    /// Merges a sub-layer node into the current context.
    /// </summary>
    /// <param name="subLayer">The sub-layer node to merge.</param>
    /// <param name="overrideNode">Whether to override an existing node.</param>
    /// <returns>The current instance of the context builder.</returns>
    /// <exception cref="LSArgumentNullException">Thrown when subLayer is null.</exception>
    public LSEventContextBuilder Merge(ILSEventLayerNode subLayer, SubContextBuilder? subContextBuilder = null) {
        if (subLayer == null) throw new LSArgumentNullException(nameof(subLayer), "Sub-layer node cannot be null.");
        // If there is no current context, allow seeding with a non-empty sub-layer
        if (_currentNode == null) {
            if (subLayer.GetChildren().Length == 0) throw new LSException("No current context to merge the sub-layer into. Make sure to start with a layer node (Sequence, Selector, Parallel).");
            var seeded = subContextBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;
            _currentNode = seeded;
            _rootNode = seeded;
            return this;
        }
        ILSEventLayerNode node = subContextBuilder?.Invoke(new LSEventContextBuilder(subLayer)).Build() ?? subLayer;

        // Entry merge: check if merging into root itself
        if (_currentNode.NodeID == node.NodeID) {
            if (_currentNode.GetType() == node.GetType()) {
                // same type: merge contents
                mergeRecursive((ILSEventLayerNode)_currentNode, node);
            } else {
                // different types: replace root container
                _currentNode = node;
                _rootNode = node;
            }
            return this;
        }

        var existing = _currentNode.GetChild(node.NodeID);
        if (existing == null) {
            _currentNode.AddChild(node);
            return this;
        }

        if (existing is ILSEventLayerNode existingLayer && node is ILSEventLayerNode subLayerNode) {
            // If container types differ, replace; otherwise merge recursively
            if (existing.GetType() != node.GetType()) {
                _currentNode.RemoveChild(node.NodeID);
                _currentNode.AddChild(node);
            } else {
                mergeRecursive(existingLayer, subLayerNode);
            }
        } else {
            // Different types or non-layer nodes: replace
            _currentNode.RemoveChild(node.NodeID);
            _currentNode.AddChild(node);
        }
        return this; // Return the builder for method chaining
    }

    private void mergeRecursive(ILSEventLayerNode currentNode, ILSEventLayerNode subNode) {
        // Iterate through all children of the source node
        foreach (var subNodeChild in subNode.GetChildren()) {
            var existingChild = currentNode.GetChild(subNodeChild.NodeID);

            if (existingChild != null) {
                // Node exists, check if both are layer nodes for recursive merging
                if (existingChild is ILSEventLayerNode existingLayer && subNodeChild is ILSEventLayerNode subNodeLayer) {
                    // Both are layer nodes - merge recursively
                    mergeRecursive(existingLayer, subNodeLayer);
                } else if (existingChild.GetType() == subNodeChild.GetType()) {
                    // Same type but not layer nodes (e.g., both handlers) - replace with subNodeChild
                    currentNode.RemoveChild(subNodeChild.NodeID);
                    currentNode.AddChild(subNodeChild);
                } else {
                    // Different types - replace with subNodeChild
                    currentNode.RemoveChild(subNodeChild.NodeID);
                    currentNode.AddChild(subNodeChild);
                }
            } else {
                // Node doesn't exist
                // Add the entire subNodeChild directly
                // I don't think this should be cloned, when builder is used it should assume that all nodes are already either clones of global nodes or new nodes
                currentNode.AddChild(subNodeChild);
            }
        }
    }
    protected bool _hasBuilt = false;
    public ILSEventLayerNode Build() {
        if (_hasBuilt) throw new LSException("Build can only be called once per builder instance.");
        _hasBuilt = true;
        if (_rootNode != null) return _rootNode;
        // If nothing was built, throw to match expected behavior
        throw new LSException("No current node to build. Make sure to end all layers.");
    }

    private bool tryGetContextChild(string nodeID, out ILSEventNode? child) {
        child = null;
        if (_currentNode == null) return false;
        child = _currentNode.GetChild(nodeID);
        return child != null;
    }


    // findNodeRecursive probably will not be used anymore, but leaving it here for reference
    // private ILSEventNode? findNodeRecursive(ILSEventNode? node, string targetNodeID) {
    //     if (node == null) return null;
    //     if (node.NodeID == targetNodeID) return node;

    //     if (node is ILSEventLayerNode layerNode) {
    //         var found = layerNode.GetChild(targetNodeID);
    //         if (found != null) return found;

    //         // Deep search in children
    //         foreach (var child in layerNode.GetChildren()) {
    //             var deepFound = findNodeRecursive(child, targetNodeID);
    //             if (deepFound != null) return deepFound;
    //         }
    //     }

    //     return null;
    // }
}
