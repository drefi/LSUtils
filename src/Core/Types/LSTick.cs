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
    protected LSProcessManager? _manager;
    public readonly float TICK_VALUE;
    public string ClassName => nameof(LSTick);
    public System.Guid ID { get; protected set; }
    public float DeltaFactor { get; protected set; } = 1;

    protected LSTick(float tickValue) {
        TICK_VALUE = tickValue;
        ID = System.Guid.NewGuid();
    }

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitializeSequence = null, LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
        _manager = manager ?? LSProcessManager.Singleton;
        var process = new TickInitializeProcess(this);
        return process
            .WithProcessing(builder => builder
                .Handler(LSProcessLabels.IS_INITIALIZED_KEY,
                handler: session => {
                    if (_isInitialized) return LSProcessResultStatus.CANCELLED;
                    _isInitialized = true;
                    _isPaused = true;
                    _tickCount = 0;
                    _resetTickCount = 0;
                    _tickTimer = 0f;
                    return LSProcessResultStatus.SUCCESS;
                }, priority: LSProcessPriority.CRITICAL)
                .Sequence(nameof(onInitializeSequence), onInitializeSequence)
        ).Execute(_manager ?? LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL);
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
            var process = new TickProcess(this, _tickCount);
            process.WithProcessing(root => root
                .Handler(LSProcessLabels.IS_INITIALIZED_KEY, session => LSProcessResultStatus.FAILURE,
                    priority: LSProcessPriority.CRITICAL,
                    conditions: (proc) => _isInitialized == false)
            ).Execute(_manager ?? LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL);
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
        if (_isInitialized == false) return;
        _isPaused = false;

    }

    /// <summary>
    /// Toggles the pause state of the tick manager.
    /// </summary>
    /// <remarks>
    /// If the tick manager has not been initialized, this method does nothing.
    /// </remarks>
    public void TogglePause() {
        if (_isInitialized == false) return;
        _isPaused = !_isPaused;
    }

    /// <summary>
    /// Sets the delta factor, which scales the time delta used during the update.
    /// 
    /// </summary>
    /// <param name="value">The new delta factor to set.</param>
    public void SetDeltaFactor(float value) {
        if (DeltaFactor == value) return;
        DeltaFactor = value;


    }

    public void Cleanup() {
        // No cleanup needed for CleanEventDispatcher
    }

    public class TickInitializeProcess : LSProcess {
        public LSTick TickManager { get; }

        public TickInitializeProcess(LSTick tickManager) {
            TickManager = tickManager;
        }
    }
    public class TickProcess : LSProcess {
        public LSTick TickManager { get; }
        public int TickCount { get; }

        public TickProcess(LSTick tickManager, int tickCount) {
            TickManager = tickManager;
            TickCount = tickCount;
        }
    }
}
