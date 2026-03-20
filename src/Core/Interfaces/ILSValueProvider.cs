namespace LSUtils;

public interface ILSValueProvider {
    void SetValue<TValue>(TValue value, params object?[] parameters);
    TValue? GetValue<TValue>(params object?[] parameters);
    bool TryGetValue<TValue>(out TValue? value, params object?[] parameters);
}
