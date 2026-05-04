namespace LSUtils.StateMachine;

using System;
using System.Collections.Generic;
using LSUtils.BehaviorTree;

/// <summary>
/// Base state node for use with <see cref="LSUtilHSM"/>.
/// <para>
/// Implement state behavior by overriding <see cref="OnSetup"/>, <see cref="OnEnter"/>,
/// <see cref="OnExit"/>, and <see cref="OnUpdate"/>. Public lifecycle methods are
/// non-virtual so lifecycle events are always emitted.
/// </para>
/// </summary>
public class LSUtilState {
    readonly Dictionary<string, List<Func<object?, bool>>> _eventHandlers = new(StringComparer.Ordinal);
    Func<object?, bool>? _guard;

    public const string EventFinished = "finished";

    /// <summary>The name that identifies this state within its parent HSM.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// The parent HSM that owns this state.
    /// Set automatically when the state is registered via <see cref="LSUtilHSM.AddState"/>.
    /// </summary>
    public LSUtilHSM? Hsm { get; internal set; }

    /// <summary>
    /// The contextual agent passed through from <see cref="LSUtilHSM.Start"/> or <see cref="Setup"/>.
    /// Available after <see cref="Setup"/> is called.
    /// </summary>
    public object? Agent { get; internal set; }

    /// <summary>
    /// Shared key/value store for this state hierarchy.
    /// Child states receive this reference from the owning HSM.
    /// </summary>
    public Blackboard Blackboard { get; private set; } = new();

    /// <summary>
    /// Plan used to initialize the root blackboard when no parent blackboard is provided.
    /// </summary>
    public BlackboardPlan BlackboardPlan { get; private set; } = new();

    /// <summary>Raised when this state is entered.</summary>
    public event Action? Entered;

    /// <summary>Raised when this state is exited.</summary>
    public event Action? Exited;

    /// <summary>Raised when this state is set up.</summary>
    public event Action? SetupCompleted;

    /// <summary>Raised when this state receives update.</summary>
    public event Action<float>? Updated;

    protected LSUtilState(string name) {
        Name = name;
    }

    /// <summary>
    /// Called once when the HSM is started, before the first <see cref="Enter"/>.
    /// Calls <see cref="OnSetup"/> and then emits <see cref="SetupCompleted"/>.
    /// </summary>
    public void Setup(object? agent) {
        Agent = agent;
        OnSetup(agent);
        SetupCompleted?.Invoke();
    }

    /// <summary>
    /// Called when this state becomes the active state in its parent HSM.
    /// Calls <see cref="OnEnter"/> and then emits <see cref="Entered"/>.
    /// </summary>
    public void Enter() {
        OnEnter();
        Entered?.Invoke();
    }

    /// <summary>
    /// Called when this state is deactivated, either by a transition or by the HSM exiting.
    /// Calls <see cref="OnExit"/> and then emits <see cref="Exited"/>.
    /// </summary>
    public void Exit() {
        OnExit();
        Exited?.Invoke();
    }

    /// <summary>
    /// Called each frame while this state is the active state in its parent HSM.
    /// Calls <see cref="OnUpdate"/> and then emits <see cref="Updated"/>.
    /// </summary>
    public void Update(float delta) {
        OnUpdate(delta);
        Updated?.Invoke(delta);
    }

    /// <summary>Override for one-time initialization logic.</summary>
    protected virtual void OnSetup(object? agent) { }

    /// <summary>Override for enter logic.</summary>
    protected virtual void OnEnter() { }

    /// <summary>Override for exit logic.</summary>
    protected virtual void OnExit() { }

    /// <summary>Override for per-frame update logic.</summary>
    protected virtual void OnUpdate(float delta) { }

    /// <summary>Registers a local event handler. Returning true consumes the event.</summary>
    public void AddEventHandler(string @event, Func<object?, bool> handler) {
        ArgumentException.ThrowIfNullOrWhiteSpace(@event);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_eventHandlers.TryGetValue(@event, out var handlers)) {
            handlers = new List<Func<object?, bool>>();
            _eventHandlers[@event] = handlers;
        }
        handlers.Add(handler);
    }

    /// <summary>Dispatches an event from this state up toward the root state machine.</summary>
    public bool Dispatch(string @event, object? cargo = null) {
        if (Hsm is not null) {
            return Hsm.DispatchFrom(this, @event, cargo);
        }
        return TryHandleEvent(@event, cargo);
    }

    /// <summary>Returns the root state in this hierarchy.</summary>
    public LSUtilState GetRoot() {
        var root = this;
        while (root.Hsm is not null) {
            root = root.Hsm;
        }
        return root;
    }

    /// <summary>Returns true when this state is currently active in its parent hierarchy.</summary>
    public bool IsActive() {
        if (Hsm is null) {
            return true;
        }

        if (!ReferenceEquals(Hsm.ActiveState, this)) {
            return false;
        }

        return Hsm.IsActive();
    }

    /// <summary>Chainable helper to rename this state before registration.</summary>
    public LSUtilState Named(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Hsm is not null) {
            throw new InvalidOperationException("Cannot rename a state after it has been added to an HSM.");
        }
        Name = name;
        return this;
    }

    /// <summary>Sets a transition guard that must pass before entering this state.</summary>
    public void SetGuard(Func<object?, bool> guardCallable) {
        ArgumentNullException.ThrowIfNull(guardCallable);
        _guard = guardCallable;
    }

    /// <summary>Sets a transition guard without cargo input.</summary>
    public void SetGuard(Func<bool> guardCallable) {
        ArgumentNullException.ThrowIfNull(guardCallable);
        _guard = _ => guardCallable();
    }

    /// <summary>Clears the previously assigned transition guard.</summary>
    public void ClearGuard() => _guard = null;

    /// <summary>Chains an action into the enter lifecycle signal.</summary>
    public LSUtilState CallOnEnter(Action callback) {
        ArgumentNullException.ThrowIfNull(callback);
        Entered += callback;
        return this;
    }

    /// <summary>Chains an action into the exit lifecycle signal.</summary>
    public LSUtilState CallOnExit(Action callback) {
        ArgumentNullException.ThrowIfNull(callback);
        Exited += callback;
        return this;
    }

    /// <summary>Chains an action into the update lifecycle signal.</summary>
    public LSUtilState CallOnUpdate(Action<float> callback) {
        ArgumentNullException.ThrowIfNull(callback);
        Updated += callback;
        return this;
    }

    internal void SetBlackboardContext(Blackboard blackboard, BlackboardPlan blackboardPlan) {
        Blackboard = blackboard;
        BlackboardPlan = blackboardPlan;
    }

    internal bool CanEnter(object? cargo) => _guard?.Invoke(cargo) ?? true;

    internal bool TryHandleEvent(string @event, object? cargo) {
        if (!_eventHandlers.TryGetValue(@event, out var handlers)) {
            return false;
        }

        foreach (var handler in handlers) {
            if (handler(cargo)) {
                return true;
            }
        }

        return false;
    }
}
