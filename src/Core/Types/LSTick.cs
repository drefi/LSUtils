namespace LSUtils;

using Logging;
using ProcessSystem;
/// <summary>
/// Manages clock and notifies any registered listeners when the clock ticks.
/// </summary>
/// TODO: Tests
/// TODO: Logging according to LSLogger standards
/// TODO: LSTick does not need to be a class, instead it should a static class that manages the tick process
/// 
public static class LSTick {
    private const float DEFAULT_TICK_VALUE = 1f;
    private static float _tickTimer;
    private static int _tickCount;
    private static int _cycleCount;
    private static bool _isInitialized;
    private static bool _isPaused;
    private static LSProcessManager? _manager;
    public static float TICK_VALUE { get; private set; }
    public static string ClassName => nameof(LSTick);
    public static float DeltaFactor { get; private set; } = 1f;

    public static void Initialize(LSProcessManager? manager = null, float tickValue = DEFAULT_TICK_VALUE) {
        if (_isInitialized) return;
        _isInitialized = true;
        _tickTimer = 0f;
        _tickCount = 0;
        _cycleCount = 0;
        _isPaused = false;
        _manager = manager ?? LSProcessManager.Singleton;
        DeltaFactor = 1f;
        TICK_VALUE = tickValue;
    }
    public static void Update(double delta) {
        if (!_isInitialized || _isPaused) return;

        float deltaTick = (float)delta * DeltaFactor;
        _tickTimer += deltaTick;
        float percentage = (float)(_tickTimer / TICK_VALUE);
        if (percentage >= 1f) {
            _tickTimer = 0;
            _tickCount++;
            if (_tickCount >= int.MaxValue - 1) {
                _cycleCount++;
                _tickCount = 0;
            }
            var process = new TickProcess(_tickCount, _cycleCount);
            var result = process.Execute(_manager, LSProcessManager.LSProcessContextMode.GLOBAL);
        }
    }
    public static void Register(LSProcessBuilderAction builder) {
        if (!_isInitialized) return;
        _manager?.Register<LSTick.TickProcess>(builder);
    }

    /// <summary>
    /// Toggles the pause state of the tick manager.
    /// </summary>
    /// <remarks>
    /// If the tick manager has not been initialized, this method does nothing.
    /// </remarks>
    public static void TogglePause(bool? pauseState = null) {
        if (_isInitialized == false) return;
        if (pauseState.HasValue) {
            _isPaused = pauseState.Value;
        } else {
            _isPaused = !_isPaused;
        }
    }

    /// <summary>
    /// Sets the delta factor, which scales the time delta used during the update.
    /// 
    /// </summary>
    /// <param name="value">The new delta factor to set.</param>
    public static void ChangeDeltaFactor(float value) {
        if (DeltaFactor == value) return;
        DeltaFactor = value;


    }
    public class TickProcess : LSProcess {
        public int TickCount { get; }
        public int CycleCount { get; }

        internal TickProcess(int tickCount, int cycleCount) {
            TickCount = tickCount;
            CycleCount = cycleCount;
        }
    }
}
