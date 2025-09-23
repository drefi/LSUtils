using System;
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
        LSEventContextBuilder contextBuilder;
        // if no instance is provided, we register a global context.
        if (instance == null) {
            if (!eventDict.TryGetValue(GlobalEventable.Instance, out var globalNode)) {
                // no global context registered for this event type, create a new parallel node using the eventType as nodeID.
                contextBuilder = new LSEventContextBuilder().Parallel($"{eventType.Name}");
            } else {
                contextBuilder = new LSEventContextBuilder(globalNode);
            }
        } else { // if an instance is provided, we register a context specific to that instance.
                 // check if we have a context already registered for this instance.            
            if (!eventDict.TryGetValue(instance, out var instanceNode)) {
                instanceNode = new LSEventContextBuilder().Parallel($"{eventType.Name}").Build();
            }
            contextBuilder = new LSEventContextBuilder(instanceNode);
        }
        var result = builder(contextBuilder);
        if (result == null) {
            throw new ArgumentNullException(nameof(builder), "The context builder delegate returned null.");
        }
        eventDict[instance ?? GlobalEventable.Instance] = result.Build();

    }

    public ILSEventLayerNode GetContext<TEvent>(ILSEventable? instance = null, ILSEventLayerNode? localContext = null) where TEvent : ILSEvent {
        return GetContext(typeof(TEvent), instance, localContext);
    }
    public ILSEventLayerNode GetContext(System.Type eventType, ILSEventable? instance = null, ILSEventLayerNode? localContext = null) {
        // if eventType is not registered, create a new event dictionary for this type.
        if (!_globalContext.TryGetValue(eventType, out var eventDict)) {
            eventDict = new Dictionary<ILSEventable, ILSEventLayerNode>();
            _globalContext[eventType] = eventDict;
        }
        LSEventContextBuilder builder = new LSEventContextBuilder().Sequence($"root");
        if (!eventDict.TryGetValue(GlobalEventable.Instance, out var globalNode)) {
            // no global context registered for this event type; create a new root parallel node.
            builder.Parallel($"{eventType.Name}");
        } else {
            // we have a globalContext; start with a clone of the global node to avoid modifying the original.
            builder.Merge(globalNode.Clone());
        }
        // merge instance specific context if available. We always use the clone of the node, so we don't modify the original instance node.
        if (instance != null) {
            if (!eventDict.TryGetValue(instance, out var instanceNode)) {
                builder.Parallel($"{eventType.Name}");
            } else {
                builder.Merge(instanceNode.Clone());
            }
        }
        // local context is merged last, so it has priority over global and instance contexts.
        if (localContext != null) {
            builder.Merge(localContext);
        }

        return builder.Build();
    }

    protected class GlobalEventable : ILSEventable {
        static GlobalEventable _instance = new();
        internal static GlobalEventable Instance => _instance;

        public System.Guid ID => Guid.NewGuid();

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
