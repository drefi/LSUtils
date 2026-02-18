namespace LSUtils;

using System;
public class LSEvaluator<T> : ILSOperandVisitor where T : System.Numerics.INumber<T> {
    private readonly ILSVariableProvider _variableProvider;
    public LSEvaluator(ILSVariableProvider variableProvider) {
        _variableProvider = variableProvider;
    }
    public float Visit(ILSConstantOperand<float> node) => node.Value;
    public bool Visit(ILSBooleanOperand node) => node.Value;
    public float Visit(ILSVarOperand<float> node) => _variableProvider.GetValue<float>(node.ID, node.Key);
    public float Visit(ILSBinaryOperand<float> node) => ILSNumericOperand<float>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public float Visit(ILSUnaryOperand<float> node) => ILSNumericOperand<float>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public float Visit(ILSTernaryConditionalOperand<float> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public int Visit(ILSConstantOperand<int> node) => node.Value;
    public int Visit(ILSVarOperand<int> node) => _variableProvider.GetValue<int>(node.ID, node.Key);
    public int Visit(ILSBinaryOperand<int> node) => ILSNumericOperand<int>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public int Visit(ILSUnaryOperand<int> node) => ILSNumericOperand<int>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public int Visit(ILSTernaryConditionalOperand<int> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public bool Visit(ILSConditionalOperand node) {
        var leftValue = (IComparable)node.Left.Resolve<object>(this);
        var rightValue = (IComparable)node.Right.Resolve<object>(this);
        return ILSBooleanOperand.ComparisonOperation(node.Operator, leftValue.CompareTo(rightValue));
    }
    public bool Visit(ILSBinaryConditionalOperand node) => ILSBooleanOperand.BooleanOperation(node.Operator, node.Left, node.Right, this);
    public bool Visit(ILSNegateBooleanOperand node) => !node.Operand.Resolve(this);
}
