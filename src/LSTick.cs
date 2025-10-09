namespace LSUtils;

using Logging;
using ProcessSystem;
/// <summary>
/// Manages clock and notifies any registered listeners when the clock ticks.
/// </summary>
/// TODO: Tests
/// TODO: Logging according to LSLogger standards
/// TODO: LSProcess needs review
public class LSTick : ILSProcessable {
    public static LSTick Singleton { get; } = new LSTick(DEFAULT_TICK_VALUE);
    const float DEFAULT_TICK_VALUE = 1f;
    public const string START_LABEL = "start";
    public const string UPDATE_LABEL = "update";
    public const string PHYSICS_UPDATE_LABEL = "physics_update";
    public const string DELTA_CHANGE_LABEL = "delta_change";
    public const string TOGGLE_PAUSE_LABEL = "toggle_pause";
    protected int _tickCount;
    protected int _resetTickCount;
    protected bool _isInitialized;
    protected bool _isPaused;
    protected double _tickTimer;
    public readonly float TICK_VALUE;
    public string ClassName => nameof(LSTick);
    public System.Guid ID { get; protected set; }
    public float DeltaFactor { get; protected set; } = 1;

    protected LSTick(float tickValue) {
        TICK_VALUE = tickValue;
        ID = System.Guid.NewGuid();
    }

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? initBuilder = null, LSProcessManager? manager = null) {
        var process = new TickProcess(this, ILSProcessable.INITIALIZE_LABEL);
        return process
            .WithProcessing(builder => builder
                .Handler("isInitialized",
                handler: (session) => {
                    if (_isInitialized) return LSProcessResultStatus.CANCELLED;
                    _isInitialized = true;
                    _isPaused = true;
                    _tickCount = 0;
                    _resetTickCount = 0;
                    _tickTimer = 0f;
                    return LSProcessResultStatus.SUCCESS;
                }, priority: LSProcessPriority.CRITICAL)
                .Selector(ILSProcessable.INITIALIZE_LABEL,
                selectorBuilder: initBuilder)
        ).Execute(this);
    }
    /// <summary>
    /// Updates the tick count and notifies listeners of tick updates.
    /// </summary>
    /// <param name="delta">The time since the last update.</param>
    /// <exception cref="LSException">Thrown if any errors occur during the update.</exception>
    public void Update(double delta) {
        if (_isInitialized == false || _isPaused) return;

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
            var process = new TickProcess(this, UPDATE_LABEL);
            process.SetData("tickCount", _tickCount);
            process.WithProcessing(builder => builder
                .Selector(UPDATE_LABEL, selectorBuilder: builder => builder,
                priority: LSProcessPriority.LOW,
                overrideConditions: true,
                conditions: (proc, node) => proc is TickProcess tickProc && tickProc.Action == UPDATE_LABEL)
            ).Execute(this);
        }
    }

    /// <summary>
    /// Updates the physics tick and notifies listeners of physics updates.
    /// </summary>
    /// <param name="delta">The time since the last physics update.</param>
    /// <exception cref="LSException">Thrown if any errors occur during the update.</exception>
    public void PhysicsUpdate(double delta) {
        float percentage = (float)(_tickTimer / TICK_VALUE);
        if (_isInitialized == false || _isPaused) return;
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
        // if (_isInitialized == false) {
        //     throw new LSException($"{ClassName}::Start::not_initialized");
        // }
        // _isPaused = false;
        var process = new TickProcess(this, START_LABEL);
        var result = process.WithProcessing(
            builder => builder
                .Handler("isInitialized",
                    handler: (session) => {
                        if (_isInitialized == false) {
                            LSLogger.Singleton.Warning($"Tick Manager is not initialized.", $"{nameof(LSTick)}.Start", session.SessionID);
                            return LSProcessResultStatus.CANCELLED;
                        }
                        _isPaused = false;
                        LSLogger.Singleton.Info($"Tick Manager started.", $"{nameof(LSTick)}.Start", session.SessionID);
                        return LSProcessResultStatus.SUCCESS;
                    },
                    priority: LSProcessPriority.CRITICAL)
                .Selector(START_LABEL,
                    selectorBuilder: builder => builder,
                    priority: LSProcessPriority.NORMAL,
                    overrideConditions: true,
                    conditions: (proc, node) => proc is TickProcess tickProc && tickProc.Action == START_LABEL)
            ).Execute(this);
    }

    /// <summary>
    /// Toggles the pause state of the tick manager.
    /// </summary>
    /// <remarks>
    /// If the tick manager has not been initialized, this method does nothing.
    /// </remarks>
    public void TogglePause() {
        // if (_isInitialized == false) return;
        // _isPaused = !_isPaused;
        // var result = new OnTogglePauseEvent(this, _isPaused).Execute(this, _manager);
        var process = new TickProcess(this, START_LABEL);
        var result = process.WithProcessing(
            builder => builder
                .Handler("isInitialized",
                    handler: (session) => {
                        if (_isInitialized == false) {
                            LSLogger.Singleton.Warning($"Tick Manager is not initialized.", $"{nameof(LSTick)}.TogglePause", session.SessionID);
                            return LSProcessResultStatus.CANCELLED;
                        }
                        _isPaused = !_isPaused;
                        LSLogger.Singleton.Info($"Tick Manager {(_isPaused ? "paused" : "resumed")}.", $"{nameof(LSTick)}.TogglePause", session.SessionID);
                        return LSProcessResultStatus.SUCCESS;
                    },
                    priority: LSProcessPriority.CRITICAL)
                .Selector(START_LABEL,
                    selectorBuilder: builder => builder,
                    priority: LSProcessPriority.NORMAL,
                    overrideConditions: true,
                    conditions: (proc, node) => proc is TickProcess tickProc && tickProc.Action == START_LABEL)
            ).Execute(this);
    }

    /// <summary>
    /// Sets the delta factor, which scales the time delta used during the update.
    /// 
    /// </summary>
    /// <param name="value">The new delta factor to set.</param>
    public void SetDeltaFactor(float value) {
        // if (DeltaFactor == value) return;
        // DeltaFactor = value;
        // var result = new OnChangeDeltaFactorEvent(this, DeltaFactor, _isPaused).Execute(this, _manager);
        var process = new TickProcess(this, DELTA_CHANGE_LABEL);
        process.SetData("oldDeltaFactor", DeltaFactor);
        process.SetData("newDeltaFactor", value);
        var result = process.WithProcessing(
            builder => builder
                .Handler("isInitialized",
                    handler: (session) => {
                        if (session.Process is not TickProcess proc) {
                            LSLogger.Singleton.Warning($"Tick Manager process is invalid.", $"{nameof(LSTick)}.SetDeltaFactor", session.SessionID);
                            return LSProcessResultStatus.CANCELLED;
                        }
                        if (_isInitialized == false) {
                            LSLogger.Singleton.Warning($"Tick Manager is not initialized.", $"{nameof(LSTick)}.SetDeltaFactor", session.SessionID);
                            return LSProcessResultStatus.CANCELLED;
                        }
                        if (!proc.TryGetData<float>("oldDeltaFactor", out var oldDelta)) {
                            LSLogger.Singleton.Warning($"Tick Manager could not get old delta factor value.", $"{nameof(LSTick)}.SetDeltaFactor", session.SessionID);
                            oldDelta = 1f;
                        }
                        if (!proc.TryGetData<float>("newDeltaFactor", out var newDelta)) {
                            LSLogger.Singleton.Warning($"Tick Manager could not get new delta factor value.", $"{nameof(LSTick)}.SetDeltaFactor", session.SessionID);
                            newDelta = 1f;
                        }
                        LSLogger.Singleton.Info($"Tick Manager delta factor changed from {oldDelta} to {newDelta}.", $"{nameof(LSTick)}.SetDeltaFactor", session.SessionID);
                        return LSProcessResultStatus.SUCCESS;
                    },
                    priority: LSProcessPriority.CRITICAL)
                .Selector(DELTA_CHANGE_LABEL,
                    selectorBuilder: builder => builder,
                    priority: LSProcessPriority.NORMAL,
                    overrideConditions: true,
                    conditions: (proc, node) => proc is TickProcess tickProc && tickProc.Action == DELTA_CHANGE_LABEL)
            ).Execute(this);
    }

    public void Cleanup() {
        // No cleanup needed for CleanEventDispatcher
    }

    public class TickProcess : LSProcess {
        public LSTick TickManager { get; }
        public string Action { get; }

        public TickProcess(LSTick tickManager, string action) {
            TickManager = tickManager;
            Action = action;
        }
    }
}
