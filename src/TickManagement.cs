using System;

namespace LSUtils;

/// <summary>
/// Manages the in-game clock and notifies any registered listeners when the clock ticks.
/// </summary>
public class TickManagement : ILSEventable {
    static TickManagement? _instance;
    public static TickManagement Instance {
        get {
            if (_instance == null) _instance = new TickManagement(DEFAULT_TICK_VALUE);
            return _instance;
        }
    }
    const float DEFAULT_TICK_VALUE = 1f;
    protected int _tickCount;
    protected int _resetTickCount;
    protected bool _hasInitialized;
    protected bool _isPaused;
    protected double _tickTimer;
    protected OnTickEvent? _eventInstance;
    LSEventIOptions? _managerEventOptions = new LSEventIOptions();
    public readonly float TICK_VALUE;
    public string ClassName => nameof(TickManagement);
    public System.Guid ID { get; protected set; }
    public int DeltaFactor { get; protected set; } = 1;

    protected TickManagement(float tickValue) {
        TICK_VALUE = tickValue;
        ID = System.Guid.NewGuid();
    }

    public bool Initialize(LSEventIOptions? eventOptions = null) {
        eventOptions ??= new LSEventIOptions();
        _managerEventOptions = new LSEventIOptions() {
            Dispatcher = eventOptions.Dispatcher,
        };
        _managerEventOptions.ErrorHandler += eventOptions.error;
        var @event = OnInitializeEvent.Create(this, eventOptions);
        @event.SuccessCallback += () => {
            _hasInitialized = true;
            _isPaused = true;
            _tickCount = 0;
            _resetTickCount = 0;
            _tickTimer = 0f;
            tickEvent(_tickCount);
        };
        return @event.Dispatch();
    }
    protected bool tickEvent(int tickCount) {
        _eventInstance?.Cancel();
        _eventInstance = new OnTickEvent(tickCount, _managerEventOptions!);
        return _eventInstance.Dispatch();
    }
    /// <summary>
    /// Updates the tick count and notifies listeners of tick updates.
    /// </summary>
    /// <param name="delta">The time since the last update.</param>
    /// <exception cref="LSException">Thrown if any errors occur during the update.</exception>
    public void Update(double delta) {
        if (_hasInitialized == false || _isPaused) return;

        double deltaTick = (float)delta * DeltaFactor;
        _tickTimer += deltaTick;
        float percentage = (float)(_tickTimer / TICK_VALUE);
        if (percentage > 1f) {
            percentage = 0f;
            _tickTimer = 0;
            _tickCount++;
            if (_tickCount == int.MaxValue) {
                _resetTickCount++;
                _tickCount = 0;
            }
            tickEvent(_tickCount);
        }
        _eventInstance?.update(deltaTick, percentage, _managerEventOptions);
    }
    /// <summary>
    /// Updates the physics tick and notifies listeners of physics updates.
    /// </summary>
    /// <param name="delta">The time since the last physics update.</param>
    /// <exception cref="LSException">Thrown if any errors occur during the update.</exception>
    public void PhysicsUpdate(double delta) {
        float percentage = (float)(_tickTimer / TICK_VALUE);
        if (_hasInitialized == false || _isPaused) return;
        double deltaTick = (float)delta * DeltaFactor;
        _eventInstance?.physicsUpdate(deltaTick, percentage, _managerEventOptions);
    }
    /// <summary>
    /// Starts the tick manager.
    /// </summary>
    /// <remarks>
    /// If the tick manager has not been initialized, this method throws an LSException.
    /// </remarks>
    /// <exception cref="LSException">Thrown if the tick manager has not been initialized.</exception>
    public void Start() {
        if (_hasInitialized == false) {
            throw new LSException($"{ClassName}::Start::not_initialized");
        }
        _isPaused = false;
    }

    /// <summary>
    /// Toggles the pause state of the tick manager.
    /// </summary>
    /// <remarks>
    /// If the tick manager has not been initialized, this method does nothing.
    /// </remarks>
    public void TogglePause() {
        if (_hasInitialized == false) return;
        _isPaused = !_isPaused;
        OnPauseEvent @event = new OnPauseEvent(_isPaused, _managerEventOptions!);
        @event.Dispatch();
    }

    /// <summary>
    /// Sets the delta factor, which scales the time delta used during the update.
    /// 
    /// </summary>
    /// <param name="value">The new delta factor to set.</param>
    public void SetDeltaFactor(int value) {
        if (DeltaFactor == value) return;
        DeltaFactor = value;
        OnChangeDeltaFactorEvent @event = new OnChangeDeltaFactorEvent(DeltaFactor, _isPaused, _managerEventOptions!);
        @event.Dispatch();
    }

    public void Cleanup() {
        throw new NotImplementedException();
    }

    #region Events
    public class OnTickEvent : LSEvent {
        public event LSTickUpdateHandler? OnTickUpdateEvent;
        public event LSTickUpdateHandler? OnTickPhysicsUpdateEvent;
        public int TickCount { get; protected set; }
        internal OnTickEvent(int tickCount, LSEventOptions eventOptions) : base(eventOptions) => TickCount = tickCount;
        internal void update(double deltaTick, float percentage, LSEventOptions? options = null) {
            OnTickUpdateEvent?.Invoke(deltaTick, percentage, options);
        }
        internal void physicsUpdate(double deltaTick, float percentage, LSEventOptions? options = null) {
            OnTickPhysicsUpdateEvent?.Invoke(deltaTick, percentage, options);
        }
    }
    public class OnChangeDeltaFactorEvent : LSEvent {
        public int Speed { get; protected set; }
        public bool IsPaused { get; protected set; }
        public OnChangeDeltaFactorEvent(int speed, bool isPaused, LSEventOptions options) : base(options) {
            Speed = speed;
            IsPaused = isPaused;
        }
    }

    public class OnPauseEvent : LSEvent {
        public bool IsPaused { get; protected set; }
        public OnPauseEvent(bool isPaused, LSEventOptions options) : base(options) => IsPaused = isPaused;
    }
    #endregion
}
