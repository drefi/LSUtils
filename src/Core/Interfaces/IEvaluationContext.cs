namespace LSUtils;

/// <summary>
/// Contexto para avaliação de operandos.
/// Fornece acesso a metadados.
/// </summary>
public interface IEvaluationContext {
    void AddData<TData>(object key, TData value) where TData : notnull;
    TData? GetData<TData>(object key);
    bool TryGetData<TData>(object key, out TData value);
}