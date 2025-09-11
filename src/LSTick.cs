using System;
using LSUtils.EventSystem;

namespace LSUtils;

/// <summary>
/// Manages clock and notifies any registered listeners when the clock ticks.
/// </summary>
public class LSTick : ILSEventable {
    public static LSTick Singleton { get; } = new LSTick(DEFAULT_TICK_VALUE);
    const float DEFAULT_TICK_VALUE = 1f;
    protected int _tickCount;
    protected int _resetTickCount;
    protected bool _hasInitialized;
    protected bool _isPaused;
    protected double _tickTimer;
    public readonly float TICK_VALUE;
    public string ClassName => nameof(LSTick);
    public System.Guid ID { get; protected set; }
    public int DeltaFactor { get; protected set; } = 1;

    public LSDispatcher? Dispatcher { get; protected set; }


    protected LSTick(float tickValue) {
        TICK_VALUE = tickValue;
        ID = System.Guid.NewGuid();
    }

    public EventProcessResult Initialize(LSEventOptions options) {
        Dispatcher = options.Dispatcher;
        return OnInitializeEvent.Create<LSTick>(this, options)
            .OnCompleted((evt) => {
                _hasInitialized = true;
                _isPaused = true;
                _tickCount = 0;
                _resetTickCount = 0;
                _tickTimer = 0f;
                tickEvent(_tickCount, new LSEventOptions(options.Dispatcher, this));
            })
            .Dispatch();
    }

    protected EventProcessResult tickEvent(int tickCount, LSEventOptions? options) {
        var @event = new OnTickEvent(this, tickCount, options);
        return @event.Dispatch();
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
            tickEvent(_tickCount, new LSEventOptions(Dispatcher, this));
        }
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
        var @event = new OnPauseEvent(this, _isPaused, new LSEventOptions(Dispatcher, this));
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
        var @event = new OnChangeDeltaFactorEvent(this, DeltaFactor, _isPaused, new LSEventOptions(Dispatcher, this));
        @event.Dispatch();
    }

    public void Cleanup() {
        // No cleanup needed for CleanEventDispatcher
    }

    #region Events

    public class OnTickEvent : LSEvent<LSTick> {
        public int TickCount { get; }

        public OnTickEvent(LSTick tickManager, int tickCount, LSEventOptions? options) : base(tickManager, options) {
            TickCount = tickCount;
        }
    }

    public class OnChangeDeltaFactorEvent : LSEvent<LSTick> {
        public int Speed { get; }
        public bool IsPaused { get; }

        public OnChangeDeltaFactorEvent(LSTick tickManager, int speed, bool isPaused, LSEventOptions? options) : base(tickManager, options) {
            Speed = speed;
            IsPaused = isPaused;
        }
    }

    public class OnPauseEvent : LSEvent<LSTick> {
        public bool IsPaused { get; }

        public OnPauseEvent(LSTick tickManager, bool isPaused, LSEventOptions? options) : base(tickManager, options) {
            IsPaused = isPaused;
        }
    }
    #endregion
}
