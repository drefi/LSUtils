namespace LSUtils;

public interface ILSValueProvider {
    void SetValue<TValue>(TValue value, params object?[] args);
    TValue? GetValue<TValue>(params object?[] args);
    bool TryGetValue<TValue>(out TValue? value, params object?[] args);
}
