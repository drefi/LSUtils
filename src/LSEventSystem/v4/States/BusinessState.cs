using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

/// <summary>
/// Business state implementation for v4.
/// Handles the sequential execution of business phases: VALIDATE -> CONFIGURE -> EXECUTE -> CLEANUP.
/// </summary>
public partial class BusinessState : IEventSystemState {
    protected EventSystemContext _context;
    PhaseState? _currentPhase = null;
    List<PhaseState> _phases = new();
    protected Dictionary<PhaseState, PhaseProcessResult?> _phaseResults = new();
    public StateProcessResult StateResult { get; protected set; } = StateProcessResult.UNKNOWN;
    //public bool IsWaiting => _currentPhaseState?.IsWaiting ?? false;
    public bool HasFailures => _phaseResults.Values.Any(r => r == PhaseProcessResult.FAILURE);
    public bool HasCancelled => _phaseResults.Values.Any(r => r == PhaseProcessResult.CANCELLED);
    public BusinessState(EventSystemContext context) {
        _context = context;
        //handlers are ordered and selected by type in the phase itself
        var handlers = _context.Handlers.OfType<PhaseHandlerEntry>().ToList();
        var validate = new ValidatePhaseState(this, handlers);
        _phases.Add(validate);
        _phases.Add(new ConfigurePhaseState(this, handlers));
        _phases.Add(new ExecutePhaseState(this, handlers));
        _phases.Add(new CleanupPhaseState(this, handlers));
        _currentPhase = validate;
    }

    internal PhaseState? getPhaseState(System.Type type) {
        return _phases.FirstOrDefault(p => p.GetType() == type);
    }
    internal T? getPhaseState<T>() where T : PhaseState {
        return getPhaseState(typeof(T)) as T;
    }
    internal bool tryGetPhaseState<T>(out T? phaseState) where T : PhaseState {
        phaseState = getPhaseState<T>();
        return phaseState != null;
    }
    //do both Process and Resume logic
    IEventSystemState phaseProcess(Func<PhaseState?> callback) {
        if (_currentPhase == null) return new SucceedState(_context); //no phases left to process
        var nextPhase = callback();
        var processPhaseResult = _currentPhase.PhaseResult;
        _phaseResults[_currentPhase] = processPhaseResult;
        switch (processPhaseResult) {
            case PhaseProcessResult.CANCELLED: //when phase is cancelled move to cancelled state
                StateResult = StateProcessResult.CANCELLED;
                _currentPhase = null;
                return new CancelledState(_context);
            case PhaseProcessResult.FAILURE: //when phase fails move to completed state
                if (nextPhase == null) {
                    StateResult = StateProcessResult.FAILURE;
                    _currentPhase = null;
                    return new CompletedState(_context);
                }
                break;
            case PhaseProcessResult.WAITING: //this can happen when is the last phase but a handler is still waiting
                StateResult = StateProcessResult.WAITING;
                return this;
        }
        _currentPhase = nextPhase;
        return this;
    }

    public IEventSystemState Process() {
        IEventSystemState result = this;
        if (_currentPhase == null) return new SucceedState(_context); //all phases completed successfully move to succeed state
        do {
            result = phaseProcess(_currentPhase.Process);
        } while (_currentPhase != null);

        return result; //all phases completed successfully move to succeed state
    }
    public IEventSystemState Resume() {
        IEventSystemState result = this;
        if (_currentPhase == null) throw new LSException("Cannot resume business state with no active phase.");
        do {
            result = phaseProcess(_currentPhase.Resume);
        } while (_currentPhase != null);

        return result; //all phases completed successfully move to succeed state
    }
    public IEventSystemState Cancel() {
        var result = _currentPhase?.Cancel();
        return new CancelledState(_context);
    }
    public IEventSystemState Fail() {
        return this;
    }
}
