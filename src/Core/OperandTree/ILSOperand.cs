namespace LSUtils;
/// <summary>
/// Base interface for operands that can be evaluated to produce a value.
/// Forms the foundation of a composable formula system.
/// </summary>
public interface ILSOperand : ILSAcceptor {
    bool Accept<TValue>(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters);
}
/// <summary>
/// Generic operand interface that produces a strongly-typed result.
/// </summary>
/// <typeparam name="TValue">The type of value this operand evaluates to.</typeparam>
public interface ILSOperand<TValue> : ILSOperand {
    bool Accept(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters);
}
