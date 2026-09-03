using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LSUtils.ProcessSystem;

/// <summary>Read-only access to one node's state in one execution. No composition API is exposed.</summary>
public sealed class LSProcessExecutionNode {
    private readonly LSProcessExecutionNode[] _children;
    private LSProcessExecutionNode[]? _eligible;
    private int _cursor;
    private bool _started;
    private bool _hasUnknown;

    public LSProcessNodeDefinition Definition { get; }
    public string NodeID => Definition.NodeID;
    public LSProcessResultStatus Status { get; private set; }
    public int ExecutionCount { get; private set; }
    public ReadOnlyCollection<LSProcessExecutionNode> Children { get; }
    public LSProcessResultStatus GetNodeStatus() => Status;
    public LSProcessExecutionNode? GetChild(string nodeID) => Array.Find(_children, child => child.NodeID == nodeID);

    internal LSProcessExecutionNode(LSProcessNodeDefinition definition) {
        Definition = definition;
        _children = definition.Children.Select(child => new LSProcessExecutionNode(child)).ToArray();
        Children = Array.AsReadOnly(_children);
    }

    internal LSProcessResultStatus Execute(LSProcessSession session) {
        if (_started || Status == LSProcessResultStatus.CANCELLED) return Status;
        _started = true;
        switch (Definition.Kind) {
            case LSProcessDefinitionNodeKind.Handler:
                var previous = session.Execution.CurrentNode;
                session.Execution.CurrentNode = this;
                try {
                    var result = Definition.Handler!(session);
                    ExecutionCount++;
                    if (Status != LSProcessResultStatus.CANCELLED) Status = result;
                    return Status;
                } finally {
                    session.Execution.CurrentNode = previous;
                }
            case LSProcessDefinitionNodeKind.Inverter:
                // Preserve the original inverter's own eligibility check; its child's
                // conditions are not evaluated as they are in sequence/selector layers.
                if (!IsEligible(Definition, session.Process)) return Status = LSProcessResultStatus.FAILURE;
                if (_children.Length == 0) return Status;
                return Status = Invert(_children[0].Execute(session));
            default:
                _eligible = _children.Where(child => IsEligible(child.Definition, session.Process))
                    .OrderByDescending(child => child.Definition.Priority)
                    .ThenBy(child => child.Definition.Order).ToArray();
                return Continue(session);
        }
    }

    internal LSProcessResultStatus Resolve(LSProcessSession session, bool success) {
        if (Status != LSProcessResultStatus.WAITING) return Status;
        if (Definition.Kind == LSProcessDefinitionNodeKind.Handler)
            return Status = success ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
        if (Definition.Kind == LSProcessDefinitionNodeKind.Inverter)
            return Status = Invert(_children[0].Resolve(session, success));

        var result = _eligible![_cursor].Resolve(session, success);
        if (ShouldStop(result)) return Status = result;
        _hasUnknown |= result == LSProcessResultStatus.UNKNOWN;
        _cursor++;
        return Continue(session);
    }

    private LSProcessResultStatus Continue(LSProcessSession session) {
        while (_cursor < _eligible!.Length) {
            if (Status == LSProcessResultStatus.CANCELLED) return Status;
            var result = _eligible[_cursor].Execute(session);
            if (Status == LSProcessResultStatus.CANCELLED) return Status;
            if (ShouldStop(result)) return Status = result;
            _hasUnknown |= result == LSProcessResultStatus.UNKNOWN;
            _cursor++;
        }
        return Status = _hasUnknown ? LSProcessResultStatus.UNKNOWN
            : Definition.Kind == LSProcessDefinitionNodeKind.Sequence
                ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
    }

    private bool ShouldStop(LSProcessResultStatus result) =>
        result == LSProcessResultStatus.WAITING || result == LSProcessResultStatus.CANCELLED ||
        result == (Definition.Kind == LSProcessDefinitionNodeKind.Sequence
            ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS);

    internal void Cancel() {
        // Preserve explicit cancellation of completed processes as supported by LSProcess.
        if (Status == LSProcessResultStatus.CANCELLED) return;
        Status = LSProcessResultStatus.CANCELLED;
        foreach (var child in _children) child.Cancel();
    }

    private static bool IsEligible(LSProcessNodeDefinition node, LSProcess process) {
        foreach (var condition in node.Conditions) {
            if (condition != null && !condition(process)) return false;
        }
        return true;
    }

    private static LSProcessResultStatus Invert(LSProcessResultStatus status) => status switch {
        LSProcessResultStatus.SUCCESS => LSProcessResultStatus.FAILURE,
        LSProcessResultStatus.FAILURE => LSProcessResultStatus.SUCCESS,
        _ => status
    };
}
