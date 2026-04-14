namespace LSUtils.OperandTree;
/// <summary>
/// Base interface for operands that can be evaluated to produce a value.
/// Forms the foundation of a composable formula system.
/// </summary>
public interface ILSOperand {
    bool Accept(ILSVisitor visitor, params object?[] args);
}
