using System;
using System.Runtime.ExceptionServices;

namespace LSUtils.ProcessSystem;

internal sealed class LSProcessExecution {
    private bool _running;
    private ExceptionDispatchInfo? _fault;
    internal Guid ID { get; } = Guid.NewGuid();
    internal LSProcessExecutionNode Root { get; }
    internal LSProcessExecutionNode? CurrentNode { get; set; }

    internal LSProcessExecution(LSProcessDefinition definition) {
        Root = new LSProcessExecutionNode(definition.Root);
    }

    internal LSProcessResultStatus Run(LSProcessSession session, bool? resolution = null) {
        _fault?.Throw();
        if (_running) throw new InvalidOperationException("Cannot execute or resolve a process reentrantly; return WAITING first.");
        _running = true;
        try {
            return resolution.HasValue ? Root.Resolve(session, resolution.Value) : Root.Execute(session);
        } catch (Exception exception) {
            _fault = ExceptionDispatchInfo.Capture(exception);
            throw;
        } finally {
            CurrentNode = null;
            _running = false;
        }
    }
}
