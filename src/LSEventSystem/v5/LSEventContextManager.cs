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
    //private readonly Dictionary<System.Type, List<NodeContextEntry>> _globalContexts = new();

    /// <summary>
    /// Get a global context for the specified event type and optional eventable instance.
    /// Merges all registered global contexts for the event type, including those specific to the instance
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="eventable"></param>
    /// <returns></returns>
    // internal ILSEventLayerNode getContext(System.Type eventType, ILSEventable? eventable = null) {
    //     // create a root parallel node to serve as the root node, only the manager have direct access to this node.
    //     //System.Console.WriteLine($"Getting context for event type {eventType.FullName} and instance {(eventable != null ? eventable.GetType().FullName : "null")}");
    //     var root = new LSEventContextBuilder()
    //         .Parallel("root"); ; // root node
    //     if (_globalContexts.TryGetValue(eventType, out var eventContexts)) {
    //         var entries = eventContexts.Where(e => e.Instance == null || e.Instance == eventable).ToList();
    //         if (entries.Count == 0) return root.Build();
    //         // all other nodes are merged into the root node, this provides a single context for the event type and instance.
    //         foreach (var entry in entries) {
    //             root.Merge(entry.Node);
    //         }
    //         return root.Build();
    //     }
    //     return root.Build();
    // }
    // public ILSEventLayerNode GetContext<TEvent>(ILSEventable? eventable = null) where TEvent : ILSEvent {
    //     return getContext(typeof(TEvent), eventable);
    // }
    // internal void registerContext(System.Type eventType, ILSEventLayerNode node, ILSEventable? instance = null) {
    //     if (!_globalContexts.TryGetValue(eventType, out var list)) {
    //         list = new List<NodeContextEntry>();
    //         _globalContexts[eventType] = list;
    //     }
    //     list.Add(new NodeContextEntry(node, instance));
    // }
    // public void RegisterContext<TEvent>(ILSEventLayerNode node, ILSEventable? instance = null) where TEvent : ILSEvent {
    //     registerContext(typeof(TEvent), node, instance);
    // }
    // internal void unregisterContext(System.Type eventType, string nodeID, ILSEventable? instance = null) {
    //     if (_globalContexts.TryGetValue(eventType, out var list)) {
    //         var entry = list.FirstOrDefault(e => e.NodeID == nodeID && e.Instance == instance);
    //         if (entry != null) {
    //             list.Remove(entry);
    //             if (list.Count == 0) {
    //                 _globalContexts.Remove(eventType);
    //             }
    //         }
    //     }
    // }
    protected readonly Dictionary<System.Type, Dictionary<ILSEventable, ILSEventLayerNode>> _globalContext = new();
    public void Register<TEvent>(LSEventContextDelegate builder, ILSEventable? instance = null) where TEvent : ILSEvent {
        Register(typeof(TEvent), builder, instance);
    }
    public void Register(System.Type eventType, LSEventContextDelegate builder, ILSEventable? instance = null) {
        if (!_globalContext.TryGetValue(eventType, out var eventDict)) {
            eventDict = new Dictionary<ILSEventable, ILSEventLayerNode>();
            _globalContext[eventType] = eventDict;
        }
        LSEventContextBuilder builderInstance;
        if (!eventDict.TryGetValue(instance ?? GlobalEventable.Instance, out var node)) {
            builderInstance = new LSEventContextBuilder()
                .Parallel("root");
            //eventDict[instance ?? GlobalEventable.Instance] = node;
        } else {
            builderInstance = new LSEventContextBuilder(node);
        }
        var result = builder(builderInstance);
        eventDict[instance ?? GlobalEventable.Instance] = result.Build();
    }

    public ILSEventLayerNode GetContext<TEvent>(ILSEventLayerNode? localContext = null, ILSEventable? instance = null) where TEvent : ILSEvent {
        return GetContext(typeof(TEvent), localContext, instance);
    }
    public ILSEventLayerNode GetContext(System.Type eventType, ILSEventLayerNode? localContext = null, ILSEventable? instance = null) {
        if (_globalContext.TryGetValue(eventType, out var eventDict)) {
            if (eventDict.TryGetValue(instance ?? GlobalEventable.Instance, out var globalContext)) {
                if (localContext != null) {
                    var builder = new LSEventContextBuilder(globalContext.Clone());
                    builder.Merge(localContext);
                    return builder.Build();
                }
                return globalContext.Clone(); // return a clone to prevent external modification, only the manager can modify the original node.
            }
        }
        return new LSEventContextBuilder()
            .Parallel("root")
            .Build();
    }

    protected class GlobalEventable : ILSEventable {
        static GlobalEventable _instance = new();
        internal static GlobalEventable Instance => _instance;

        LSEventProcessStatus ILSEventable.Initialize(LSEventContextManager manager, ILSEventLayerNode context) {
            throw new System.NotImplementedException();
        }
    }
}

// internal class NodeContextEntry {
//     public ILSEventLayerNode Node { get; }
//     public ILSEventable? Instance { get; }
//     public string NodeID => Node.NodeID;
//     public NodeContextEntry(ILSEventLayerNode node, ILSEventable? instance = null) {
//         Instance = instance;
//         Node = node;
//     }
// }
