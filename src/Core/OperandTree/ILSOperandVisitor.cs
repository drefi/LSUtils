namespace LSUtils;

public interface ILSOperandVisitor {
    float Visit(ILSConstantOperand<float> node);
    bool Visit(ILSBooleanOperand node);
    float Visit(ILSVarOperand<float> node);
    float Visit(ILSBinaryOperand<float> node);
    float Visit(ILSUnaryOperand<float> node);
    float Visit(ILSTernaryConditionalOperand<float> node);
    int Visit(ILSConstantOperand<int> node);
    int Visit(ILSVarOperand<int> node);
    int Visit(ILSBinaryOperand<int> node);
    int Visit(ILSUnaryOperand<int> node);
    int Visit(ILSTernaryConditionalOperand<int> node);
    bool Visit(ILSConditionalOperand node);
    bool Visit(ILSBinaryConditionalOperand node);
    bool Visit(ILSNegateBooleanOperand node);
}
