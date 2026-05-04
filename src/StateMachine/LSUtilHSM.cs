namespace LSUtils.StateMachine;

using System;
using System.Collections.Generic;

/// <summary>
/// Event-based Hierarchical State Machine (HSM).
/// <para>
/// Manages a set of <see cref="LSUtilState"/> instances and drives transitions between
/// them via named events. Because <see cref="LSUtilHSM"/> itself extends
/// <see cref="LSUtilState"/>, an HSM can be nested inside another HSM as a composite state.
/// </para>
/// <para>
/// Typical top-level usage:
/// <code>
/// var hsm = new LSUtilHSM("Root");
/// hsm.AddState(new MyIdleState());
/// hsm.AddState(new MyCombatState());
/// hsm.AddTransition("Idle", "enemy_spotted", "Combat");
/// hsm.AddTransition("Combat", "enemy_lost",  "Idle");
/// hsm.Start(agent: this);   // Setup + Enter
/// // Each frame:
/// hsm.Update(delta);
/// </code>
/// </para>
/// </summary>
public class LSUtilHSM : LSUtilState {
    readonly Dictionary<string, LSUtilState> _states = new();
    readonly Dictionary<(string stateName, string eventName), (string toState, Func<bool>? guard)> _transitions = new();
    readonly Dictionary<string, string> _anyTransitions = new();

    string? _initialStateName;
    LSUtilState? _activeState;
    LSUtilState? _previousActiveState;
    bool _initialized;

    /// <summary>The currently active child state, or <c>null</c> when the HSM is inactive.</summary>
    public LSUtilState? ActiveState => _activeState;

    /// <summary>The state that was active before the current one. <c>null</c> if no transition has occurred yet.</summary>
    public LSUtilState? PreviousActiveState => _previousActiveState;

    /// <summary>Returns the previously active substate.</summary>
    public LSUtilState? GetPreviousActiveState() => _previousActiveState;

    /// <summary>
    /// The initial state that will be entered when this HSM activates.
    /// <c>null</c> if no states have been registered yet.
    /// </summary>
    public LSUtilState? InitialState =>
        _initialStateName is not null && _states.TryGetValue(_initialStateName, out var s) ? s : null;

    /// <summary>All registered states, keyed by name.</summary>
    public IReadOnlyDictionary<string, LSUtilState> States => _states;

    /// <summary>Emitted whenever the active substate changes. Arguments are the new and previous states.</summary>
    public event Action<LSUtilState, LSUtilState?>? ActiveStateChanged;

    public LSUtilHSM(string name) : base(name) { }

    // -----------------------------------------------------------------------
    // State registration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a state with this HSM. The first state added becomes the initial
    /// state unless overridden with <see cref="SetInitialState"/>.
    /// If the HSM has already been started, <see cref="LSUtilState.Setup"/> is called
    /// on the new state immediately.
    /// </summary>
    public void AddState(LSUtilState state) {
        if (state.Hsm is not null) {
            throw new InvalidOperationException(
                $"State '{state.Name}' is already registered with HSM '{state.Hsm.Name}'.");
        }

        if (_states.ContainsKey(state.Name)) {
            throw new InvalidOperationException($"State '{state.Name}' already exists in HSM '{Name}'.");
        }

        state.Hsm = this;
        state.SetBlackboardContext(Blackboard, BlackboardPlan);
        _states[state.Name] = state;
        _initialStateName ??= state.Name;

        if (_initialized) {
            state.Agent = Agent;
            state.Setup(Agent);
        }
    }

    /// <summary>Sets the state that will be entered when this HSM starts or re-enters.</summary>
    public void SetInitialState(string stateName) {
        if (!_states.ContainsKey(stateName)) {
            throw new ArgumentException(
                $"State '{stateName}' not found in HSM '{Name}'.", nameof(stateName));
        }
        _initialStateName = stateName;
    }

    // -----------------------------------------------------------------------
    // Transition registration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a transition: when <paramref name="fromState"/> is active and
    /// <paramref name="eventName"/> is dispatched, activate <paramref name="toState"/>.
    /// An optional per-transition <paramref name="guard"/> may be supplied; if it returns
    /// <c>false</c> the transition is skipped (state-level guards are evaluated separately).
    /// </summary>
    public void AddTransition(string fromState, string eventName, string toState, Func<bool>? guard = null) {
        _transitions[(fromState, eventName)] = (toState, guard);
    }

    /// <summary>
    /// Registers a wildcard transition that triggers from any active state.
    /// Specific transitions (registered via <see cref="AddTransition"/>) take priority.
    /// </summary>
    public void AddAnyTransition(string eventName, string toState) {
        _anyTransitions[eventName] = toState;
    }

    /// <summary>Removes a specific transition from <paramref name="fromState"/> triggered by <paramref name="eventName"/>.</summary>
    public void RemoveTransition(string fromState, string eventName) {
        _transitions.Remove((fromState, eventName));
    }

    /// <summary>Returns <c>true</c> if a transition from <paramref name="fromState"/> on <paramref name="eventName"/> is registered.</summary>
    public bool HasTransition(string fromState, string eventName) =>
        _transitions.ContainsKey((fromState, eventName));

    /// <summary>
    /// Forces a transition to the named state regardless of the active event flow.
    /// If the target is already active it will still be exited and re-entered.
    /// Does not evaluate per-transition or state-level guards.
    /// </summary>
    public void ChangeActiveState(string stateName) {
        if (!_states.TryGetValue(stateName, out var nextState)) {
            throw new ArgumentException(
                $"State '{stateName}' not found in HSM '{Name}'.", nameof(stateName));
        }

        var previous = _activeState;
        _activeState?.Exit();
        _previousActiveState = previous;
        _activeState = nextState;
        _activeState.Enter();
        ActiveStateChanged?.Invoke(_activeState, previous);
    }

    /// <summary>
    /// Returns the deepest currently active state within this hierarchy.
    /// If this HSM's active state is itself a nested <see cref="LSUtilHSM"/>, recurses into it.
    /// </summary>
    public LSUtilState? GetLeafState() {
        if (_activeState is LSUtilHSM subHsm) {
            return subHsm.GetLeafState();
        }
        return _activeState;
    }

    // -----------------------------------------------------------------------
    // Event dispatch
    // -----------------------------------------------------------------------

    /// <summary>
    /// Dispatches a named event through the hierarchy. Event handlers and transitions
    /// are separate mechanisms: handlers react without changing state; transitions switch
    /// the active state.
    /// <para>Dispatch order:</para>
    /// <list type="number">
    ///   <item>Active state's <b>event handlers</b> (recurses into sub-HSMs leaf-first).</item>
    ///   <item>Active state's <b>specific transitions</b> registered on this HSM.</item>
    ///   <item>This HSM's own <b>event handlers</b>.</item>
    ///   <item><b>Any-transitions</b> (wildcard, any active state).</item>
    ///   <item>Bubbles to the <b>parent HSM</b> if still unhandled.</item>
    /// </list>
    /// Returns <c>true</c> if the event was consumed at any step.
    /// </summary>
    public bool DispatchEvent(string eventName, object? cargo = null) {
        if (_activeState is not null) {
            // Step 1 — active state's event handlers (recurse into sub-HSMs).
            if (_activeState is LSUtilHSM activeSubHsm) {
                if (activeSubHsm.DispatchEvent(eventName, cargo)) {
                    return true;
                }
            } else if (_activeState.TryHandleEvent(eventName, cargo)) {
                return true;
            }

            // Step 2 — specific transitions for the active state.
            if (_transitions.TryGetValue((_activeState.Name, eventName), out var specificEntry)
                && TryTransitionTo(specificEntry.toState, specificEntry.guard, cargo)) {
                return true;
            }
        }

        // Step 3 — this HSM's own event handlers.
        if (TryHandleEvent(eventName, cargo)) {
            return true;
        }

        // Step 4 — any-transitions.
        if (_anyTransitions.TryGetValue(eventName, out var anyTarget)
            && TryTransitionTo(anyTarget, null, cargo)) {
            return true;
        }

        // Step 5 — bubble to parent HSM.
        return Hsm?.DispatchFrom(this, eventName, cargo) ?? false;
    }

    internal bool DispatchFrom(LSUtilState source, string eventName, object? cargo = null) {
        if (!ReferenceEquals(source, this)) {
            if (source.TryHandleEvent(eventName, cargo)) {
                return true;
            }

            if (_transitions.TryGetValue((source.Name, eventName), out var directEntry)
                && TryTransitionTo(directEntry.toState, directEntry.guard, cargo)) {
                return true;
            }
        }

        if (TryHandleEvent(eventName, cargo)) {
            return true;
        }

        if (_anyTransitions.TryGetValue(eventName, out var anyTarget)
            && TryTransitionTo(anyTarget, null, cargo)) {
            return true;
        }

        return Hsm?.DispatchFrom(this, eventName, cargo) ?? false;
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Convenience method for top-level use: calls <see cref="Setup"/> then <see cref="Enter"/>.
    /// Equivalent to starting a standalone HSM as its own root.
    /// </summary>
    public void Start(object? agent = null) {
        Setup(agent);
        Enter();
    }

    /// <inheritdoc/>
    protected override void OnSetup(object? agent) {
        if (Hsm is null) {
            SetBlackboardContext(BlackboardPlan.CreateBlackboard(), BlackboardPlan);
        }

        foreach (var state in _states.Values) {
            state.Agent = agent;
            state.SetBlackboardContext(Blackboard, BlackboardPlan);
            state.Setup(agent);
        }
        _initialized = true;
    }

    /// <summary>
    /// Activates this HSM by entering its initial state.
    /// Call <see cref="Setup"/> (or <see cref="Start"/>) before calling this.
    /// </summary>
    protected override void OnEnter() {
        if (_initialStateName is null) {
            throw new InvalidOperationException(
                $"HSM '{Name}' has no states. Add at least one state before starting.");
        }
        if (!TryTransitionTo(_initialStateName, null, cargo: null)) {
            throw new InvalidOperationException(
                $"Initial state '{_initialStateName}' is guarded and cannot be entered in HSM '{Name}'.");
        }
    }

    /// <summary>Deactivates this HSM by exiting the current active state.</summary>
    protected override void OnExit() {
        _activeState?.Exit();
        _activeState = null;
    }

    /// <inheritdoc/>
    protected override void OnUpdate(float delta) {
        _activeState?.Update(delta);
    }

    // -----------------------------------------------------------------------
    // Internal
    // -----------------------------------------------------------------------

    bool TryTransitionTo(string stateName, Func<bool>? transitionGuard, object? cargo) {
        if (!_states.TryGetValue(stateName, out var nextState)) {
            throw new InvalidOperationException(
                $"Transition target '{stateName}' not found in HSM '{Name}'.");
        }

        if (transitionGuard is not null && !transitionGuard()) {
            return false;
        }

        if (!nextState.CanEnter(cargo)) {
            return false;
        }

        var previous = _activeState;
        _previousActiveState = previous;
        _activeState?.Exit();
        _activeState = nextState;
        _activeState.Enter();
        ActiveStateChanged?.Invoke(_activeState, previous);
        return true;
    }
}
