namespace LSUtils.ProcessSystem;

using System;

/// <summary>Context for one execution. Typed views share this execution, not a new tree.</summary>
public class LSProcessSession {
    public const string ClassName = nameof(LSProcessSession);
    internal LSProcessExecution Execution { get; }
    public LSProcessDefinition Definition { get; }
    public LSProcessExecutionNode RootNode => Execution.Root;
    public LSProcessExecutionNode? CurrentNode => Execution.CurrentNode;
    public Guid SessionID => Execution.ID;
    internal bool IsRunning => Execution.IsRunning;
    public LSProcessManager Manager { get; }
    public LSProcess Process { get; }
    private readonly ILSProcessable[]? _instances;
    private readonly ILSProcessable[]? _contextInstances;
    public ILSProcessable[]? Instances => (ILSProcessable[]?)_instances?.Clone();
    public ILSProcessable[]? ContextInstances => (ILSProcessable[]?)_contextInstances?.Clone();
    public LSProcessManager.LSProcessContextMode ContextMode { get; }

    internal LSProcessSession(LSProcessManager manager, LSProcess process, ILSProcessNode rootNode,
        LSProcessManager.LSProcessContextMode behaviour, ILSProcessable[]? instances, ILSProcessable[]? contextInstances)
        : this(manager, process, LSProcessDefinition.Compile(rootNode), behaviour, instances, contextInstances) { }

    internal LSProcessSession(LSProcessManager manager, LSProcess process, LSProcessDefinition definition,
        LSProcessManager.LSProcessContextMode behaviour, ILSProcessable[]? instances, ILSProcessable[]? contextInstances) {
        Manager = manager;
        Process = process;
        Definition = definition;
        Execution = new LSProcessExecution(definition);
        ContextMode = behaviour;
        _instances = (ILSProcessable[]?)instances?.Clone();
        _contextInstances = (ILSProcessable[]?)contextInstances?.Clone();
    }

    internal LSProcessSession(LSProcessManager manager, LSProcess process, LSProcessDefinition definition,
        LSProcessManager.LSProcessContextMode behaviour, ILSProcessable[]? instances,
        ILSProcessable[]? contextInstances, LSProcessExecutionMemento memento,
        LSProcessPayloadCodecRegistry codecs) {
        Manager = manager;
        Process = process;
        Definition = definition;
        Execution = new LSProcessExecution(definition, memento, codecs);
        ContextMode = behaviour;
        _instances = (ILSProcessable[]?)instances?.Clone();
        _contextInstances = (ILSProcessable[]?)contextInstances?.Clone();
    }

    internal LSProcessSession(LSProcessSession session) {
        Manager = session.Manager;
        Process = session.Process;
        Definition = session.Definition;
        Execution = session.Execution;
        ContextMode = session.ContextMode;
        _instances = session._instances;
        _contextInstances = session._contextInstances;
    }

    internal LSProcessResult Execute() => Execution.Run(this);
    public LSProcessExecutionMemento CaptureExecution(LSProcessPayloadCodecRegistry codecs) {
        ArgumentNullException.ThrowIfNull(codecs);
        return Execution.Capture(Definition, Process.ID, Process.CreatedAt, codecs);
    }
    public LSProcessResult Resume() => Execution.Run(this, LSProcessResult.Success);
    public LSProcessResult Resume<T>(T payload) => Execution.Run(this, LSProcessResult.Succeeded(payload));
    public LSProcessResult Fail() => Execution.Run(this, LSProcessResult.Failure);
    public LSProcessResult Fail<T>(T payload) => Execution.Run(this, LSProcessResult.Failed(payload));
    public LSProcessResult Cancel() {
        RootNode.Cancel(LSProcessResult.Cancelled);
        return RootNode.Status;
    }
    public LSProcessResult Cancel<T>(T payload) {
        RootNode.Cancel(LSProcessResult.CancelledBy(payload));
        return RootNode.Status;
    }
}
