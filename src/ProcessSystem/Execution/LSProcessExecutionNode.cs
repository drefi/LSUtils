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
    private LSProcessResult _lastResult;

    public LSProcessNodeDefinition Definition { get; }
    public string NodeID => Definition.NodeID;
    public LSProcessResult Status { get; private set; }
    public int ExecutionCount { get; private set; }
    public ReadOnlyCollection<LSProcessExecutionNode> Children { get; }
    public LSProcessResult GetNodeStatus() => Status;
    public LSProcessExecutionNode? GetChild(string nodeID) => Array.Find(_children, child => child.NodeID == nodeID);

    internal LSProcessExecutionNode(LSProcessNodeDefinition definition) {
        Definition = definition;
        _children = definition.Children.Select(child => new LSProcessExecutionNode(child)).ToArray();
        Children = Array.AsReadOnly(_children);
    }

    internal LSProcessResult Execute(LSProcessSession session) {
        if (_started || Status == LSProcessResult.Cancelled) return Status;
        _started = true;
        switch (Definition.Kind) {
            case LSProcessDefinitionNodeKind.Handler:
                var previous = session.Execution.CurrentNode;
                session.Execution.CurrentNode = this;
                try {
                    var result = Definition.Handler!(session);
                    ExecutionCount++;
                    if (Status != LSProcessResult.Cancelled) Status = result;
                    return Status;
                } finally {
                    session.Execution.CurrentNode = previous;
                }
            case LSProcessDefinitionNodeKind.Inverter:
                // Preserve the original inverter's own eligibility check; its child's
                // conditions are not evaluated as they are in sequence/selector layers.
                if (!IsEligible(Definition, session.Process)) return Status = LSProcessResult.Failure;
                if (_children.Length == 0) return Status = LSProcessResult.Undetermined;
                return Status = Invert(_children[0].Execute(session));
            default:
                _eligible = _children.Where(child => IsEligible(child.Definition, session.Process))
                    .OrderByDescending(child => child.Definition.Priority)
                    .ThenBy(child => child.Definition.Order).ToArray();
                return Continue(session);
        }
    }

    internal LSProcessResult Resolve(LSProcessSession session, LSProcessResult resolution) {
        if (!Status.IsWaiting) return Status;
        if (Definition.Kind == LSProcessDefinitionNodeKind.Handler)
            return Status = resolution;
        if (Definition.Kind == LSProcessDefinitionNodeKind.Inverter)
            return Status = Invert(_children[0].Resolve(session, resolution));

        var result = _eligible![_cursor].Resolve(session, resolution);
        _lastResult = result;
        if (ShouldStop(result)) return Status = result;
        _hasUnknown |= result.IsUndetermined;
        _cursor++;
        return Continue(session);
    }

    private LSProcessResult Continue(LSProcessSession session) {
        while (_cursor < _eligible!.Length) {
            if (Status == LSProcessResult.Cancelled) return Status;
            var result = _eligible[_cursor].Execute(session);
            _lastResult = result;
            if (Status == LSProcessResult.Cancelled) return Status;
            if (ShouldStop(result)) return Status = result;
            _hasUnknown |= result.IsUndetermined;
            _cursor++;
        }
        if (_hasUnknown) return Status = LSProcessResult.Undetermined;
        if (Definition.Kind == LSProcessDefinitionNodeKind.Sequence)
            return Status = _lastResult.IsSuccess ? _lastResult : LSProcessResult.Success;
        return Status = _lastResult.IsFailure ? _lastResult : LSProcessResult.Failure;
    }

    private bool ShouldStop(LSProcessResult result) =>
        result == LSProcessResult.Waiting || result == LSProcessResult.Cancelled ||
        result == (Definition.Kind == LSProcessDefinitionNodeKind.Sequence
            ? LSProcessResult.Failure : LSProcessResult.Success);

    internal void Cancel(LSProcessResult cancellation) {
        if (!cancellation.IsCancelled)
            throw new ArgumentException("Cancellation must carry a cancelled result.", nameof(cancellation));
        // Preserve explicit cancellation of completed processes as supported by LSProcess.
        if (Status.IsCancelled) return;
        Status = cancellation;
        foreach (var child in _children) child.Cancel(cancellation);
    }

    private static bool IsEligible(LSProcessNodeDefinition node, LSProcess process) {
        foreach (var condition in node.Conditions) {
            if (condition != null && !condition(process)) return false;
        }
        return true;
    }

    private static LSProcessResult Invert(LSProcessResult result) {
        if (result.IsSuccess) return LSProcessResult.Failed(new LSProcessInversion(result));
        if (result.IsFailure) return LSProcessResult.Succeeded(new LSProcessInversion(result));
        return result;
    }
}
