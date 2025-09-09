using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Fluent builder for configuring event handlers with nested callback blocks.
/// Handlers are registered immediately but builder provides Register() for explicit completion.
/// </summary>
public class LSEventRegister_v3<TEvent> where TEvent : ILSEvent {
    private readonly LSDispatcher_v3 _dispatcher;

    internal LSEventRegister_v3(LSDispatcher_v3 dispatcher) {
        _dispatcher = dispatcher;
    }

    #region Phase-Based Registration

    /// <summary>
    /// Registers handlers for the VALIDATE phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnValidatePhase(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.VALIDATE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the PREPARE phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnPreparePhase(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.PREPARE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the EXECUTE phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnExecutePhase(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.EXECUTE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the SUCCESS phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnSuccess(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.SUCCESS);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the FAILURE phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnFailure(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.FAILURE);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the CANCEL phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnCancel(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.CANCEL);
        configurePhase(phaseBuilder);
        return this;
    }

    /// <summary>
    /// Registers handlers for the COMPLETE phase with nested callback support.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnComplete(Action<LSPhaseBuilder_v3<TEvent>> configurePhase) {
        var phaseBuilder = new LSPhaseBuilder_v3<TEvent>(_dispatcher, LSEventPhase.COMPLETE);
        configurePhase(phaseBuilder);
        return this;
    }

    #endregion

    #region Simple Direct Registration

    /// <summary>
    /// Registers a sequential handler for a specific phase.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnSequential(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(phase, handler, LSHandlerExecutionMode_v3.Sequential, priority);
        return this;
    }

    /// <summary>
    /// Registers a parallel handler for a specific phase.
    /// </summary>
    public LSEventRegister_v3<TEvent> OnParallel(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSESPriority priority = LSESPriority.NORMAL) {
        registerHandler(phase, handler, LSHandlerExecutionMode_v3.Parallel, priority);
        return this;
    }

    #endregion

    #region Registration Completion

    /// <summary>
    /// This should be the only method that can "finalize" the builder.
    /// </summary>
    public void Register() {
        
    }

    #endregion

    private void registerHandler(LSEventPhase phase, Func<TEvent, LSHandlerResult> handler, LSHandlerExecutionMode_v3 mode, LSESPriority priority, Func<TEvent, bool>? condition = null) {
        var entry = new LSHandlerEntry_v3 {
            Phase = phase,
            Priority = priority,
            ExecutionMode = mode,
            Handler = evt => handler((TEvent)evt),
            Condition = condition != null ? evt => condition((TEvent)evt) : null
        };
        
        _dispatcher.registerHandler<TEvent>(entry);
    }
}
