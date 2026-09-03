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

    internal LSProcessSession(LSProcessSession session) {
        Manager = session.Manager;
        Process = session.Process;
        Definition = session.Definition;
        Execution = session.Execution;
        ContextMode = session.ContextMode;
        _instances = session._instances;
        _contextInstances = session._contextInstances;
    }

    internal LSProcessResultStatus Execute() => Execution.Run(this);
    public LSProcessResultStatus Resume() => Execution.Run(this, true);
    public LSProcessResultStatus Fail() => Execution.Run(this, false);
    public LSProcessResultStatus Cancel() {
        RootNode.Cancel();
        return RootNode.Status;
    }
}
