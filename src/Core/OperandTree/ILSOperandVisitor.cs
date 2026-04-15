namespace LSUtils.OperandTree;

public interface ILSOperandVisitor : ILSVisitor {
    bool Visit(ILSNumericOperand node, out object? result, params object?[] args);
    bool Visit<TValue>(ILSNumericOperand<TValue> node, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSConstantOperand<TValue> node, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSBinaryOperand<TValue> node, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSUnaryOperand<TValue> node, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSTernaryConditionalOperand<TValue> node, out TValue? result, params object?[] args) where TValue : System.Numerics.INumber<TValue>;

    bool Visit(ILSBooleanOperand node, out bool? result, params object?[] args);
    bool Visit(ILSComparerOperand node, out bool? result, params object?[] args);
    bool Visit(ILSConditionalOperand node, out bool? result, params object?[] args);
    bool Visit(ILSNegateBooleanOperand node, out bool? result, params object?[] args);
}
