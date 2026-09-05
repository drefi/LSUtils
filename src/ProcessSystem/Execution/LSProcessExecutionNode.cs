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

    internal LSProcessExecutionNodeMemento Capture(LSProcessPayloadCodecRegistry codecs) => new(
        NodeID,
        Definition.Kind,
        _started,
        _cursor,
        _hasUnknown,
        ExecutionCount,
        _eligible?.Select(eligible => Array.IndexOf(_children, eligible)).ToArray() ?? Array.Empty<int>(),
        Status.Capture(codecs),
        _lastResult.Capture(codecs),
        _children.Select(child => child.Capture(codecs)).ToArray());

    internal void Restore(LSProcessExecutionNodeMemento memento, LSProcessPayloadCodecRegistry codecs) {
        if (memento.NodeId != NodeID || memento.Kind != Definition.Kind ||
            memento.Children.Count != _children.Length) {
            throw new InvalidOperationException($"Execution memento does not match process node '{NodeID}'.");
        }
        if (memento.Cursor < 0 || memento.Cursor > memento.EligibleChildren.Count) {
            throw new InvalidOperationException($"Execution cursor is invalid for process node '{NodeID}'.");
        }
        if (memento.ExecutionCount < 0) {
            throw new InvalidOperationException($"Execution count is invalid for process node '{NodeID}'.");
        }
        if (!memento.Started && (!memento.Status.Outcome.Equals(LSProcessOutcome.NotExecuted) ||
            memento.Cursor != 0 || memento.EligibleChildren.Count != 0)) {
            throw new InvalidOperationException($"Unstarted process node '{NodeID}' carries execution state.");
        }
        if (Definition.Kind is LSProcessDefinitionNodeKind.Handler or LSProcessDefinitionNodeKind.Inverter &&
            memento.EligibleChildren.Count != 0) {
            throw new InvalidOperationException($"Process node '{NodeID}' cannot carry eligible children.");
        }
        var eligible = new LSProcessExecutionNode[memento.EligibleChildren.Count];
        for (var i = 0; i < eligible.Length; i++) {
            var childIndex = memento.EligibleChildren[i];
            if (childIndex < 0 || childIndex >= _children.Length ||
                Array.IndexOf(memento.EligibleChildren.ToArray(), childIndex) != i) {
                throw new InvalidOperationException($"Eligible child state is invalid for process node '{NodeID}'.");
            }
            eligible[i] = _children[childIndex];
        }
        for (var i = 0; i < _children.Length; i++) _children[i].Restore(memento.Children[i], codecs);
        _started = memento.Started;
        _cursor = memento.Cursor;
        _hasUnknown = memento.HasUndetermined;
        ExecutionCount = memento.ExecutionCount;
        _eligible = memento.Started && Definition.Kind is not LSProcessDefinitionNodeKind.Handler and
            not LSProcessDefinitionNodeKind.Inverter ? eligible : null;
        Status = LSProcessResult.Restore(memento.Status, codecs);
        _lastResult = LSProcessResult.Restore(memento.LastResult, codecs);
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
