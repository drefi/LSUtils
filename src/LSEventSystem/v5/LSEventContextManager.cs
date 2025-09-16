using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LSUtils.EventSystem;

public class LSEventContextManager {
    private readonly Dictionary<System.Type, List<NodeContextEntry>> _globalContexts = new();
    // internal ILSEventLayerNode getContext(System.Type eventType, ILSEventable eventable) {
    //     ILSEventLayerNode context = new LSEventContextBuilder()
    //         .Sequence("root")
    //         .End()
    //         .Build();
    //     if (_globalContexts.TryGetValue(eventType, out var eventContexts)) {
    //         var entries = eventContexts.Where(e => e.Instance == null || e.Instance == eventable).ToList();
    //         foreach (var entry in entries) {
    //             var node = entry.Node.Clone();
    //             context = new LSEventContextBuilder(context)
    //                 .Splice(node)
    //                 .Build();
    //             //return entry.Node.Clone();
    //         }
    //         //return node.Clone();
    //     }
    //     //build a default sequence root context
    //     return context;
    // }

}

public class NodeContextEntry {
    public ILSEventLayerNode Node { get; }
    public ILSEventable? Instance { get; }
    public NodeContextEntry(ILSEventLayerNode node, ILSEventable? instance = null) {
        Instance = instance;
        Node = node;
    }
}
