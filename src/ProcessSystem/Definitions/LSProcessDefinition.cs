using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LSUtils.ProcessSystem;

/// <summary>Finalized topology and callbacks, independent of any execution state.</summary>
public sealed class LSProcessDefinition {
    public LSProcessNodeDefinition Root { get; }

    private LSProcessDefinition(LSProcessNodeDefinition root) {
        Root = root;
    }

    internal static LSProcessDefinition Compile(ILSProcessNode root) {
        ArgumentNullException.ThrowIfNull(root);
        return new LSProcessDefinition(Copy(root, new HashSet<ILSProcessNode>(ReferenceEqualityComparer.Instance)));
    }

    private static LSProcessNodeDefinition Copy(ILSProcessNode source, HashSet<ILSProcessNode> ancestors) {
        if (!ancestors.Add(source)) {
            throw new InvalidOperationException($"Process tree contains a cycle at '{source.NodeID}'.");
        }
        try {
            var type = source.GetType();
            var kind = type == typeof(LSProcessNodeSequence) ? LSProcessDefinitionNodeKind.Sequence
                : type == typeof(LSProcessNodeSelector) ? LSProcessDefinitionNodeKind.Selector
                : type == typeof(LSProcessNodeInverter) ? LSProcessDefinitionNodeKind.Inverter
                : type == typeof(LSProcessNodeHandler) ? LSProcessDefinitionNodeKind.Handler
                : throw new NotSupportedException($"Cannot compile process node type '{type.FullName}'.");

            var children = source is ILSProcessLayerNode layer
                ? layer.GetChildren() : Array.Empty<ILSProcessNode>();
            var copiedChildren = new LSProcessNodeDefinition[children.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < children.Length; i++) {
                var child = children[i];
                if (!ids.Add(child.NodeID)) {
                    throw new InvalidOperationException($"Duplicate child ID '{child.NodeID}' in '{source.NodeID}'.");
                }
                copiedChildren[i] = Copy(child, ancestors);
            }

            // Preserve composition order; eligibility and execution order belong to the executor.
            return new LSProcessNodeDefinition(source.NodeID, kind, source.Order, source.Priority,
                source.UpdatePolicy, source.Conditions, copiedChildren,
                (source as LSProcessNodeHandler)?.Handler);
        } finally {
            ancestors.Remove(source);
        }
    }
}

public enum LSProcessDefinitionNodeKind { Sequence, Selector, Inverter, Handler }

public sealed class LSProcessNodeDefinition {
    public string NodeID { get; }
    public LSProcessDefinitionNodeKind Kind { get; }
    public int Order { get; }
    public LSProcessPriority Priority { get; }
    public NodeUpdatePolicy UpdatePolicy { get; }
    public ReadOnlyCollection<LSProcessNodeCondition?> Conditions { get; }
    public ReadOnlyCollection<LSProcessNodeDefinition> Children { get; }
    public LSProcessHandler? Handler { get; }

    internal LSProcessNodeDefinition(string nodeID, LSProcessDefinitionNodeKind kind, int order,
        LSProcessPriority priority, NodeUpdatePolicy updatePolicy, LSProcessNodeCondition?[] conditions,
        LSProcessNodeDefinition[] children, LSProcessHandler? handler) {
        NodeID = nodeID;
        Kind = kind;
        Order = order;
        Priority = priority;
        UpdatePolicy = updatePolicy;
        Conditions = Array.AsReadOnly((LSProcessNodeCondition?[])conditions.Clone());
        Children = Array.AsReadOnly((LSProcessNodeDefinition[])children.Clone());
        Handler = handler;
    }
}
