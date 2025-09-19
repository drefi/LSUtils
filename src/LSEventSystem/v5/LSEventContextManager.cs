using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Manages global event contexts for different event types and optional eventable instances.
/// only the manager should be able to register and provide contexts for the system.
/// </summary>
public class LSEventContextManager {
    public static LSEventContextManager Singleton { get; } = new LSEventContextManager();
    private readonly Dictionary<System.Type, List<NodeContextEntry>> _globalContexts = new();

    /// <summary>
    /// Get a global context for the specified event type and optional eventable instance.
    /// Merges all registered global contexts for the event type, including those specific to the instance
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="eventable"></param>
    /// <returns></returns>
    internal ILSEventLayerNode getContext(System.Type eventType, ILSEventable? eventable = null) {

        // create a root parallel node to serve as the root node, only the manager have direct access to this node.
        System.Console.WriteLine($"Getting context for event type {eventType.FullName} and instance {(eventable != null ? eventable.GetType().FullName : "null")}");
        var root = new LSEventContextBuilder()
            .Parallel("root"); ; // root node
        if (_globalContexts.TryGetValue(eventType, out var eventContexts)) {
            var entries = eventContexts.Where(e => e.Instance == null || e.Instance == eventable).ToList();
            if (entries.Count == 0) return root.Build();
            // all other nodes are merged into the root node, this provides a single context for the event type and instance.
            foreach (var entry in entries) {
                root.Merge(entry.Node);
            }
            return root.Build();
        }
        return root.Build();
    }
    public ILSEventLayerNode GetContext<TEvent>(ILSEventable? eventable = null) where TEvent : ILSEvent {
        return getContext(typeof(TEvent), eventable);
    }
    internal void registerContext(System.Type eventType, ILSEventLayerNode node, ILSEventable? instance = null) {
        if (!_globalContexts.TryGetValue(eventType, out var list)) {
            list = new List<NodeContextEntry>();
            _globalContexts[eventType] = list;
        }
        list.Add(new NodeContextEntry(node, instance));
    }
    public void RegisterContext<TEvent>(ILSEventLayerNode node, ILSEventable? instance = null) where TEvent : ILSEvent {
        registerContext(typeof(TEvent), node, instance);
    }
    internal void unregisterContext(System.Type eventType, string nodeID, ILSEventable? instance = null) {
        if (_globalContexts.TryGetValue(eventType, out var list)) {
            var entry = list.FirstOrDefault(e => e.NodeID == nodeID && e.Instance == instance);
            if (entry != null) {
                list.Remove(entry);
                if (list.Count == 0) {
                    _globalContexts.Remove(eventType);
                }
            }
        }
    }
}

internal class NodeContextEntry {
    public ILSEventLayerNode Node { get; }
    public ILSEventable? Instance { get; }
    public string NodeID => Node.NodeID;
    public NodeContextEntry(ILSEventLayerNode node, ILSEventable? instance = null) {
        Instance = instance;
        Node = node;
    }
}
