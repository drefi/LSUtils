using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

internal record NodeResultKey(ILSEventNode? parent, ILSEventNode node);

public class LSEventProcessContext {

    protected LSEventProcessContext? _baseContext;
    public LSEvent Event { get; }
    private readonly Dictionary<NodeResultKey, LSEventProcessStatus> _handlerNodeResults = new();
    private readonly List<LSAction<LSEventProcessContext>> _onSuccessCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onFailureCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onCancelCallbacks = new();
    private volatile bool _isCancelled = false;

    public bool IsCancelled => _isCancelled;
    internal LSEventProcessContext(LSEvent @event, LSEventProcessContext? baseContext = null) {
        Event = @event;
        _baseContext = baseContext;
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
            if (oldStatus != LSEventProcessStatus.WAITING) return false; // cannot update an already finished node to a different status
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

        foreach (LSEventCondition condition in node.Conditions.GetInvocationList()) {
            if (!condition(Event, node)) {
                return LSEventProcessStatus.SUCCESS; // skip if any conditions are not met
            }
        }
        ILSEventNode? parent = null;
        var nodeResultKey = getNodeResultKey(node); // try to find existing key
        if (nodeResultKey != null) { // if key does not exist either is root node or
            parent = nodeResultKey.parent;
        }

        //"initialize" child nodes results if this is a layer node
        if (node is ILSEventLayerNode parentLayerNode) {
            foreach (var child in parentLayerNode.GetChildren()) {
                var key = getNodeResultKey(child, parentLayerNode); //explicitly set parent as the layer node
                //initialize with unknown status so that Resume/Fail know that this node was not yet processed
                registerProcessStatus(key, LSEventProcessStatus.UNKNOWN, out _);
            }
        }
        var nodeKey = getNodeResultKey(node, parent);
        // process node, the node should update its own status during processing
        var result = node.Process(this);
        // if status is cancelled during processing, Cancel the context
        if (result == LSEventProcessStatus.CANCELLED) Cancel();
        return result;
    }

    public LSEventProcessStatus Resume(LSEventHandlerNode node) {
        if (node is not LSEventHandlerNode) {
            throw new LSException("Can only resume handler nodes.");
        }
        var key = getNodeResultKey(node);
        if (key == null) throw new LSException("NodeResultKey should never be null for handler nodes.");
        if (key.parent == null) throw new LSException("Cannot resume root node.");
        // we check if status is WAITING before updating to SUCCESS
        if (registerProcessStatus(key, LSEventProcessStatus.WAITING, out var existingStatus)) {
            if (existingStatus != LSEventProcessStatus.UNKNOWN) throw new LSException("the existing status should be UNKNOWN in this case");
            // this means the node was UNKNOWN, we can just update to SUCCESS and return SUCCESS
            registerProcessStatus(key, LSEventProcessStatus.SUCCESS, out existingStatus);
            // maybe this should return UNKNOWN to indicate that the node is being resumed before processing, but for now we assume that resuming an unknown node means it succeeded
            return LSEventProcessStatus.SUCCESS;
        } else if (existingStatus == LSEventProcessStatus.WAITING) {
            // node was waiting, update the status to SUCCESS and re-process the node
            registerProcessStatus(key, LSEventProcessStatus.SUCCESS, out existingStatus);
            return Process(key.parent); // re-process the parent node to continue the flow
        }

        throw new LSException($"Invalid state: cannot resume this node {node.NodeID}.");
    }

    public LSEventProcessStatus Fail(LSEventHandlerNode node) {
        // similar to Resume but mark as FAILURE
        if (node is not LSEventHandlerNode) {
            throw new LSException("Can only fail handler nodes.");
        }
        var key = getNodeResultKey(node);
        if (key == null) throw new LSException("NodeResultKey should never be null for handler nodes.");
        if (key.parent == null) throw new LSException("Cannot fail root node.");
        // we check if status is WAITING before updating to FAILURE
        if (registerProcessStatus(key, LSEventProcessStatus.WAITING, out var existingStatus)) {
            if (existingStatus != LSEventProcessStatus.UNKNOWN) throw new LSException("the existing status should be UNKNOWN in this case");
            // this means the node was UNKNOWN, we can just update to FAILURE and return FAILURE
            registerProcessStatus(key, LSEventProcessStatus.FAILURE, out existingStatus);
            // maybe this should return UNKNOWN to indicate that the node is being failed before processing, but for now we assume that failing an unknown node means it failed
            return LSEventProcessStatus.FAILURE;
        } else if (existingStatus == LSEventProcessStatus.WAITING) {
            // node was waiting, update the status to FAILURE and re-process the node
            registerProcessStatus(key, LSEventProcessStatus.FAILURE, out existingStatus);
            return Process(key.parent); // re-process the parent node to continue the flow
        }
        throw new LSException($"Invalid state: cannot fail this node {node.NodeID}.");
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
