namespace LSUtils;

public interface ILSVariableProvider {
    object? GetValue(params object?[] parameters);
    T? GetValue<T>(params object?[] parameters) => (T?)GetValue(parameters);
}
