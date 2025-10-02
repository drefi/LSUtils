namespace LSUtils;

public interface ILSState : ILSClass {
    void Enter<T>(LSAction<T> enterCallback, LSAction<T> exitCallback) where T : ILSState;
    void Exit(LSAction callback);
}
