namespace LSUtils;
/// <summary>
/// Interface for variable operands that retrieve their value from a provider based on an ID and key.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILSVarOperand<T> : ILSNumericOperand<T> where T : System.Numerics.INumber<T> {
    /// <summary>
    /// Identifier for the variable source (e.g., "Source" / "Target"). Used by the provider to determine where to get the value from.
    /// </summary>
    string ID { get; }
    /// <summary>
    /// Key object that provides additional context for the provider to retrieve the correct value. Depends on the provider's implementation.
    /// </summary>
    object Key { get; }
}
