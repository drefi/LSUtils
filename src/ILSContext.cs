namespace LSUtils;

public interface ILSContext : ILSClass {
    void AddState<TState>(TState state) where TState : ILSState;
    TState GetState<TState>() where TState : ILSState;
    bool TryGetState<TState>(out TState state) where TState : ILSState;
    void SetState<TState>(LSAction<TState>? enterCallback = null, LSAction<TState>? exitCallback = null) where TState : ILSState;
}
