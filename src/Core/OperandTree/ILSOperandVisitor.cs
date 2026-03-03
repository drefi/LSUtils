namespace LSUtils;

public interface ILSOperandVisitor : ILSVisitor {
    //bool Visit<TValue>(ILSVarOperand node, out TValue? value, params object?[] parameters);
    bool Visit<TValue>(ILSNumericOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSConstantOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSBinaryOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSUnaryOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : System.Numerics.INumber<TValue>;
    bool Visit<TValue>(ILSTernaryConditionalOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : System.Numerics.INumber<TValue>;
    bool Visit(ILSBooleanOperand node, out bool? value, params object?[] parameters);
    bool Visit(ILSComparerOperand node, out bool? value, params object?[] parameters);
    bool Visit(ILSConditionalOperand node, out bool? value, params object?[] parameters);
    bool Visit(ILSNegateBooleanOperand node, out bool? value, params object?[] parameters);
    
}
