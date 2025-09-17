using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

internal record NodeResultKey(ILSEventNode? parent, ILSEventNode node);

public class LSEventProcessContext {

    public LSEvent Event { get; }
    private readonly Dictionary<NodeResultKey, LSEventProcessStatus> _handlerNodeResults = new();
    private readonly List<LSAction<LSEventProcessContext>> _onSuccessCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onFailureCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onCancelCallbacks = new();
    private volatile bool _isCancelled = false;

    public bool IsCancelled => _isCancelled;
    internal LSEventProcessContext(LSEvent @event) {
        Event = @event;
    }

    /// <summary>
    /// Tries to get the process status of a node.
    /// return false when is first register (LSEventProcessStatus.UNKNOWN), trying to update to UNKNOWN or if the status are the same as the old.
    /// true if status was updated
    /// </summary>
    internal bool registerProcessStatus(NodeResultKey? nodeKey, LSEventProcessStatus status, out LSEventProcessStatus oldStatus) {
        if (nodeKey == null) throw new LSException("NodeResultKey cannot be null when registering process status.");
        if (_handlerNodeResults.TryGetValue(nodeKey, out oldStatus)) {
            // already registered, ignore if status is UNKNOWN
            if (status == LSEventProcessStatus.UNKNOWN) return false; // never update to UNKNOWN
            if (oldStatus == status) return false; // no change in status
            if (oldStatus == LSEventProcessStatus.SUCCESS || oldStatus == LSEventProcessStatus.FAILURE || oldStatus == LSEventProcessStatus.CANCELLED) return false; // cannot update an already finished node to a different status
            // update status
            _handlerNodeResults[nodeKey] = status;
            return true; // return true because status changed
        }
        // first time registering this node
        _handlerNodeResults.Add(nodeKey, status);
        return false;
    }
    internal NodeResultKey? getNodeResultKey(ILSEventNode node, ILSEventNode? parent = null) {
        var key = _handlerNodeResults.FirstOrDefault(kv => kv.Key.node == node).Key;
        return key == default ? parent == null ? null : new NodeResultKey(parent, node) : key;
    }

    internal LSEventProcessStatus Process(ILSEventNode node) {
        if (IsCancelled) return LSEventProcessStatus.CANCELLED;

        // this condition is mainly for the root node, children nodes will check conditions during the parent node processing
        foreach (LSEventCondition condition in node.Conditions.GetInvocationList()) {
            if (!condition(Event, node)) {
                // since we are skipping this node at the parent or root level this means the node fails in case the conditions are not met
                return LSEventProcessStatus.FAILURE;
            }
        }
        ILSEventNode? parent = null;
        var nodeResultKey = getNodeResultKey(node); // try to find existing key
        if (nodeResultKey != null) { // if key does not exist either is root node or
            parent = nodeResultKey.parent;
        }

        // we are initializing the children keys in the parent node.Process() call
        // if (node is ILSEventLayerNode parentLayerNode) {
        //     foreach (var child in parentLayerNode.GetChildren()) {
        //         var key = getNodeResultKey(child, parentLayerNode); //explicitly set parent as the layer node
        //         //initialize with unknown status so that Resume/Fail know that this node was not yet processed
        //         registerProcessStatus(key, LSEventProcessStatus.UNKNOWN, out _);
        //     }
        // }
        var nodeKey = getNodeResultKey(node, parent);
        // process node, the node should update its own status during processing
        var result = node.Process(this);
        // if status is cancelled during processing, Cancel the context
        if (result == LSEventProcessStatus.CANCELLED) Cancel();
        return result;
    }

    public LSEventProcessStatus Resume(LSEventHandlerNode node) {
        if (node == null || node is not LSEventHandlerNode) {
            // when no node is provided, we should be able to trick the process context to resume the latest waiting node
            return LSEventProcessStatus.UNKNOWN;
        }
        var key = getNodeResultKey(node);
        if (key == null) throw new LSException("NodeResultKey should never be null for handler nodes.");
        if (key.parent == null) throw new LSException("Cannot resume root node.");
        if (_handlerNodeResults.TryGetValue(key, out var existingStatus)) {
            if (existingStatus == LSEventProcessStatus.WAITING) {
                // node was waiting, update to SUCCESS and re-process parent
                registerProcessStatus(key, LSEventProcessStatus.SUCCESS, out _);
                return Process(key.parent);
            } else if (existingStatus == LSEventProcessStatus.UNKNOWN) {
                // node was unknown, set to SUCCESS
                registerProcessStatus(key, LSEventProcessStatus.SUCCESS, out _);
                return LSEventProcessStatus.SUCCESS;
            } else {
                throw new LSException($"Cannot resume node {node.NodeID} in state {existingStatus}.");
            }
        }
        // not registered, throw exception
        throw new LSException($"Cannot resume node {node.NodeID} that was not registered.");
    }

    public LSEventProcessStatus Fail(LSEventHandlerNode node) {
        // similar to Resume but mark as FAILURE
        if (node is not LSEventHandlerNode) {
            throw new LSException("Can only fail handler nodes.");
        }
        var key = getNodeResultKey(node);
        if (key == null) throw new LSException("NodeResultKey should never be null for handler nodes.");
        if (key.parent == null) throw new LSException("Cannot fail root node.");
        if (_handlerNodeResults.TryGetValue(key, out var existingStatus)) {
            if (existingStatus == LSEventProcessStatus.WAITING) {
                // node was waiting, update to FAILURE and re-process parent
                registerProcessStatus(key, LSEventProcessStatus.FAILURE, out _);
                return Process(key.parent);
            } else if (existingStatus == LSEventProcessStatus.UNKNOWN) {
                // node was unknown, set to FAILURE
                registerProcessStatus(key, LSEventProcessStatus.FAILURE, out _);
                return LSEventProcessStatus.FAILURE;
            } else {
                throw new LSException($"Cannot fail node {node.NodeID} in state {existingStatus}.");
            }
        }
        // not registered, throw exception
        throw new LSException($"Cannot resume node {node.NodeID} that was not registered.");
    }
    public void Cancel() {
        if (IsCancelled) return; //assume that cancelled already happen (this only happens if the Cancel was actually called after the node was processed)
        _isCancelled = true;
        foreach (var node in _handlerNodeResults.Keys) {
            // mark waiting node as cancelled
            if (_handlerNodeResults[node] == LSEventProcessStatus.WAITING) {
                _handlerNodeResults[node] = LSEventProcessStatus.CANCELLED;
            }
        }
        foreach (LSAction<LSEventProcessContext> callback in _onCancelCallbacks) { // execute cancel callbacks
            callback(this);
        }
    }
}
