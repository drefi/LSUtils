namespace LSUtils.OperandTree;
/// <summary>
/// Base interface for operands that can be evaluated to produce a value.
/// Forms the foundation of a composable formula system.
/// </summary>
public interface ILSOperand {
    bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters);
}
public interface ILSOperand<TValue> : ILSOperand {
    bool Evaluate(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters);
}
