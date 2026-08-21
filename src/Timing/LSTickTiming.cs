namespace LSUtils.Timing;

using System;

public enum LSTickStartMode {
    Immediate,
    AfterInterval
}

/// <summary>
/// Defines the discrete tick schedule inside an active duration.
/// Duration and interval use the same time unit supplied by the caller.
/// </summary>
public readonly record struct LSTickTiming(float Duration, float Interval, LSTickStartMode StartMode = LSTickStartMode.Immediate) {
    public int ExpectedTickCount {
        get {
            if (Duration <= 0f || Interval <= 0f) return 0;
            var elapsedTicks = (int)MathF.Floor(Duration / Interval);
            return StartMode == LSTickStartMode.Immediate ? elapsedTicks + 1 : elapsedTicks;
        }
    }

    public LSTickTiming Validate() {
        if (!float.IsFinite(Duration) || !float.IsFinite(Interval) || Duration < 0f || Interval <= 0f)
            throw new LSArgumentException("Tick duration must be non-negative and interval must be positive.");
        return this;
    }
}

/// <summary>
/// Runtime accumulator for a validated tick timing. The caller controls the
/// active lifetime; this type only converts delta time into discrete ticks.
/// </summary>
public struct LSTickTimer {
    private readonly float _interval;
    private readonly bool _emitImmediately;
    private float _accumulator;
    private bool _started;

    public LSTickTimer(LSTickTiming timing) {
        timing.Validate();
        _interval = timing.Interval;
        _emitImmediately = timing.StartMode == LSTickStartMode.Immediate;
        _accumulator = 0f;
        _started = false;
    }

    public void Reset() {
        _accumulator = 0f;
        _started = false;
    }

    public int Advance(float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            throw new LSArgumentException("Tick delta time must be finite and non-negative.");

        var ticks = 0;
        if (!_started) {
            _started = true;
            if (_emitImmediately) ticks++;
        }

        _accumulator += deltaTime;
        while (_accumulator >= _interval) {
            _accumulator -= _interval;
            ticks++;
        }
        return ticks;
    }
}
