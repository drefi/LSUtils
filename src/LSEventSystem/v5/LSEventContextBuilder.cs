using System.Collections.Generic;

namespace LSUtils.EventSystem;

public class LSEventContextBuilder {
    private Stack<ILSEventLayerNode> _layerNodeStack = new();
    private ILSEventLayerNode? _currentNode;

    // Constructor for global mode
    internal LSEventContextBuilder() {
        //when creating a "global" context we start with no root context
    }

    // Constructor for event mode
    internal LSEventContextBuilder(ILSEventLayerNode currentNode) {
        _currentNode = currentNode;
        _layerNodeStack.Push(currentNode);
    }

    //create a node handler in the current context
    public LSEventContextBuilder Handler(string nodeID, LSEventHandler handler, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if (_layerNodeStack.Count <= 0) {
            throw new LSException("No active layer to add a handler node to. Make sure to start with a layer node (e.g., Sequence, Selector or Parallel).");
        }
        var handlerNode = LSEventHandlerNode.Create(nodeID, handler, priority, conditions);
        var context = _layerNodeStack.Peek();
        if (context.HasChild(nodeID)) {
            throw new LSException($"Node with ID '{nodeID}' already exists in the current context.");
        }
        context.AddChild(handlerNode);

        return this;
    }
    // Navigate to an existing node by its nodeID
    // var builder = new LSEventContextBuilder()
    //      .Sequence("root")
    //          .Handler("handler1", handlerFunc)
    //      .End()
    //      .Build();
    // var builder2 = new LSEventContextBuilder(builder)
    //      .Navigate("root") // Navigate to the "root" node, don't care what type of node is
    //          .Remove("handler1") // Remove the handler1 node
    //      .End() // End navigation
    //      .Build();
    public LSEventContextBuilder Navigate(string nodeID) {
        //if there is no active layer, we can't navigate
        if (_layerNodeStack.Count == 0) {
            if (_currentNode != null) {
                _layerNodeStack.Push(_currentNode);
            } else {
                throw new LSException("No active layer to navigate.");
            }
        }
        var context = _layerNodeStack.Peek();
        var node = findNodeRecursive(context, nodeID);
        if (node is not ILSEventLayerNode layerNode) {
            throw new LSException($"Node with ID '{nodeID}' not found or is not a layer node.");
        }
        _layerNodeStack.Push(layerNode);
        return this;
    }
    public LSEventContextBuilder Remove(string nodeID) {
        if (_layerNodeStack.Count == 0) {
            if (_currentNode != null) {
                _layerNodeStack.Push(_currentNode);
            } else {
                throw new LSException("No current node to remove from. Use Navigate to set the current context.");
            }
        }
        _layerNodeStack.Peek().RemoveChild(nodeID);
        return this;
    }

    public LSEventContextBuilder Sequence(string nodeID, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if ((_layerNodeStack.Count > 0 ? _layerNodeStack.Peek() : findNodeRecursive(_currentNode, nodeID)) is not ILSEventLayerNode existingLayer) {
            //root context just push the node
            var root = LSEventSequenceNode.Create(nodeID, priority, conditions);
            _layerNodeStack.Push(root);
            return this;
        }
        if (existingLayer is LSEventSequenceNode sequence && sequence.NodeID == nodeID) {
            // we already have the sequence node in the context, push to stack
            _layerNodeStack.Push(sequence);
        } else {
            // we have context but it's not a sequence, so we create a new sequence
            var sequenceNode = LSEventSequenceNode.Create(nodeID, priority, conditions);
            if (existingLayer.HasChild(nodeID)) {
                // Node with the same ID already exists in the current context
                throw new LSException($"Node with ID '{nodeID}' already exists in the current context.");
            }
            existingLayer.AddChild(sequenceNode);
            _layerNodeStack.Push(sequenceNode);

        }
        return this;
    }

    public LSEventContextBuilder Selector(string nodeID, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if ((_layerNodeStack.Count > 0 ? _layerNodeStack.Peek() : findNodeRecursive(_currentNode, nodeID)) is not ILSEventLayerNode existingLayer) {
            //root context just push the node
            var root = LSEventSelectorNode.Create(nodeID, priority, conditions);
            _layerNodeStack.Push(root);
            return this;
        }
        if (existingLayer is LSEventSelectorNode selector && selector.NodeID == nodeID) {
            // we already have the selector node in the context, push to stack
            _layerNodeStack.Push(selector);
        } else {
            // we have context but it's not a selector, so we create a new selector
            var selectorNode = LSEventSelectorNode.Create(nodeID, priority, conditions);
            if (existingLayer.HasChild(nodeID)) {
                // Node with the same ID already exists in the current context
                throw new LSException($"Node with ID '{nodeID}' already exists in the current context.");
            }
            existingLayer.AddChild(selectorNode);
            _layerNodeStack.Push(selectorNode);

        }
        return this;
    }

    public LSEventContextBuilder Parallel(string nodeID, int numRequiredToSucceed, LSPriority priority = LSPriority.NORMAL, params LSEventCondition?[] conditions) {
        if ((_layerNodeStack.Count > 0 ? _layerNodeStack.Peek() : findNodeRecursive(_currentNode, nodeID)) is not ILSEventLayerNode existingLayer) {
            //root context just push the node
            var root = LSEventParallelNode.Create(nodeID, numRequiredToSucceed, priority, conditions);
            _layerNodeStack.Push(root);
            return this;
        }
        if (existingLayer is LSEventParallelNode parallel && parallel.NodeID == nodeID) {
            // we already have the parallel node in the context, push to stack
            _layerNodeStack.Push(parallel);
        } else {
            // we have context but it's not a parallel, so we create a new parallel
            var parallelNode = LSEventParallelNode.Create(nodeID, numRequiredToSucceed, priority, conditions);
            if (existingLayer.HasChild(nodeID)) {
                // Node with the same ID already exists in the current context
                throw new LSException($"Node with ID '{nodeID}' already exists in the current context.");
            }
            existingLayer.AddChild(parallelNode);
            _layerNodeStack.Push(parallelNode);

        }
        return this;
    }

    // var root =  new LSEventContextBuilder()
    //      .Sequence("root")
    //      .End().Build();

    // var contextA = new LSEventContextBuilder()
    //      .Sequence("sequenceA")
    //          .Handler("handlerA1", handlerFuncA1)
    //      .End().Build();
    //
    // var contextB = new LSEventContextBuilder()
    //      .Sequence("sequenceB")
    //          .Handler("handlerB", handlerFuncB)
    //     .End()
    // .Build();
    //
    // var contextC = new LSEventContextBuilder()
    //      .Sequence("sequenceA")
    //          .Handler("handlerA2", handlerFuncA2)
    //      .End()
    // .Build();
    //
    // var globalContext = new LSEventContextBuilder(root)
    //     .Merge(contextA)
    //     .Merge(contextB)
    //     .Merge(contextC)
    //     .Build();
    // var localContext = new LSEventContextBuilder()
    //      .Sequence("myLocalSequence")
    //         .Sequence("myLocalSubSequence")
    //              .Handler("myLocalHandler", myLocalHandlerFunc);
    //        .End()
    //      .End()
    //      .Build();
    // var finalContext = new LSEventContextBuilder(globalContext)
    //      .Sequence("sequenceA") // navigate to the merged context
    //          .Merge(localContext)
    //      .End()
    //     .Build();
    public LSEventContextBuilder Merge(ILSEventLayerNode subLayer) {
        if (subLayer == null) {
            throw new LSArgumentNullException(nameof(subLayer));
        }

        if (_currentNode == null) {
            throw new LSException("No current node available for merging. Make sure to initialize the builder properly.");
        }

        // Check if a node with the same ID already exists
        var existingChild = _currentNode.FindChild(subLayer.NodeID);
        
        if (existingChild != null) {
            // Node exists, check if both are layer nodes for recursive merging
            if (existingChild is ILSEventLayerNode existingLayer) {
                // Both are layer nodes - merge recursively
                MergeRecursive(existingLayer, subLayer);
            }
            else {
                // Different types - replace with source
                _currentNode.RemoveChild(subLayer.NodeID);
                _currentNode.AddChild(subLayer.Clone());
            }
        } else {
            // Node doesn't exist - add cloned copy of the entire subLayer
            _currentNode.AddChild(subLayer.Clone());
        }
        
        return this; // Return the builder for method chaining
    }

    private void MergeRecursive(ILSEventLayerNode targetNode, ILSEventLayerNode sourceNode) {
        // Iterate through all children of the source node
        foreach (var sourceChild in sourceNode.GetChildren()) {
            var existingChild = targetNode.FindChild(sourceChild.NodeID);
            
            if (existingChild != null) {
                // Node exists, check if both are layer nodes for recursive merging
                if (existingChild is ILSEventLayerNode existingLayer && sourceChild is ILSEventLayerNode sourceLayer) {
                    // Both are layer nodes - merge recursively
                    MergeRecursive(existingLayer, sourceLayer);
                }
                else if (existingChild.GetType() == sourceChild.GetType()) {
                    // Same type but not layer nodes (e.g., both handlers) - replace with source
                    targetNode.RemoveChild(sourceChild.NodeID);
                    targetNode.AddChild(sourceChild.Clone());
                }
                else {
                    // Different types - replace with source
                    targetNode.RemoveChild(sourceChild.NodeID);
                    targetNode.AddChild(sourceChild.Clone());
                }
            } else {
                // Node doesn't exist - add cloned copy
                targetNode.AddChild(sourceChild.Clone());
            }
        }
    }

    public LSEventContextBuilder End() {
        _currentNode = _layerNodeStack.Pop();
        return this;
    }

    public ILSEventLayerNode Build() {
        if (_currentNode == null) throw new LSException("No current node to build. Make sure to end all layers.");
        return _currentNode;
    }

    private ILSEventNode? findNodeRecursive(ILSEventNode? node, string targetNodeID) {
        if (node == null) return null;
        if (node.NodeID == targetNodeID) return node;

        if (node is ILSEventLayerNode layerNode) {
            var found = layerNode.FindChild(targetNodeID);
            if (found != null) return found;

            // Deep search in children
            foreach (var child in layerNode.GetChildren()) {
                var deepFound = findNodeRecursive(child, targetNodeID);
                if (deepFound != null) return deepFound;
            }
        }

        return null;
    }
}
