using System;
using LSUtils.Processing;

namespace LSUtils;

/// <summary>
/// Manages clock and notifies any registered listeners when the clock ticks.
/// </summary>
public class LSTick : ILSProcessable {
    public static LSTick Singleton { get; } = new LSTick(DEFAULT_TICK_VALUE);
    const float DEFAULT_TICK_VALUE = 1f;
    protected LSProcessManager? _manager;
    protected int _tickCount;
    protected int _resetTickCount;
    protected bool _hasInitialized;
    protected bool _isPaused;
    protected double _tickTimer;
    public readonly float TICK_VALUE;
    public string ClassName => typeof(LSTick).AssemblyQualifiedName ?? nameof(LSTick);
    public System.Guid ID { get; protected set; }
    public int DeltaFactor { get; protected set; } = 1;

    protected LSTick(float tickValue) {
        TICK_VALUE = tickValue;
        ID = System.Guid.NewGuid();
    }

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? ctxBuilder = null, LSProcessManager? manager = null) {
        _manager = manager;
        OnInitializeEvent @event = new OnInitializeEvent(this);
        if (ctxBuilder != null) @event.WithProcessing(ctxBuilder);
        return @event
            .WithProcessing(b => b
                .Handler("initialize", (evt, ctx) => {
                    _hasInitialized = true;
                    _isPaused = true;
                    _tickCount = 0;
                    _resetTickCount = 0;
                    _tickTimer = 0f;
                    var result = new OnTickEvent(this, _tickCount).Execute(this, _manager);
                    return LSProcessResultStatus.SUCCESS;
                }
            )
        ).Execute(this, _manager);
    }

    // public EventProcessResult Initialize(LSEventOptions options) {
    //     Dispatcher = options.Dispatcher;
    //     return OnInitializeEvent.Create<LSTick>(this, options)
    //         .OnCompleted((evt) => {
    //             _hasInitialized = true;
    //             _isPaused = true;
    //             _tickCount = 0;
    //             _resetTickCount = 0;
    //             _tickTimer = 0f;
    //             tickEvent(_tickCount, new LSEventOptions(options.Dispatcher, this));
    //         })
    //         .Dispatch();
    // }

    // protected EventProcessResult tickEvent(int tickCount, LSEventOptions? options) {
    //     var @event = new OnTickEvent(this, tickCount, options);
    //     return @event.Dispatch();
    // }
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
            var result = new OnTickEvent(this, _tickCount).Execute(this, _manager);
            //tickEvent(_tickCount, new LSEventOptions(Dispatcher, this));
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
        var result = new OnTogglePauseEvent(this, _isPaused).Execute(this, _manager);
    }

    /// <summary>
    /// Sets the delta factor, which scales the time delta used during the update.
    /// 
    /// </summary>
    /// <param name="value">The new delta factor to set.</param>
    public void SetDeltaFactor(int value) {
        if (DeltaFactor == value) return;
        DeltaFactor = value;
        var result = new OnChangeDeltaFactorEvent(this, DeltaFactor, _isPaused).Execute(this, _manager);
    }

    public void Cleanup() {
        // No cleanup needed for CleanEventDispatcher
    }

    #region Events

    public class OnInitializeEvent : LSProcess {
        public LSTick TickManager { get; }

        public OnInitializeEvent(LSTick tickManager) {
            TickManager = tickManager;
        }
    }
    public class OnTickEvent : LSProcess {
        public int TickCount { get; }
        public LSTick TickManager { get; }

        public OnTickEvent(LSTick tickManager, int tickCount) {
            TickCount = tickCount;
            TickManager = tickManager;
        }
    }

    public class OnChangeDeltaFactorEvent : LSProcess {
        public int Speed { get; }
        public bool IsPaused { get; }
        public LSTick TickManager { get; }

        public OnChangeDeltaFactorEvent(LSTick tickManager, int speed, bool isPaused) {
            Speed = speed;
            IsPaused = isPaused;
            TickManager = tickManager;
        }
    }

    public class OnTogglePauseEvent : LSProcess {
        public bool IsPaused { get; }
        public LSTick TickManager { get; }

        public OnTogglePauseEvent(LSTick tickManager, bool isPaused) {
            IsPaused = isPaused;
            TickManager = tickManager;
        }
    }
    #endregion
}
