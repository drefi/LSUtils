using System.Linq;

namespace LSUtils;

public class LSEvaluator : ILSOperandVisitor {
    protected ILSVariableProvider _variableProvider;
    public LSEvaluator(ILSVariableProvider variableProvider) {
        _variableProvider = variableProvider;
    }

    public virtual float Visit(ILSConstantOperand<float> node) => node.Value;
    public virtual bool Visit(ILSBooleanOperand node) => node.Value;
    public virtual object? Visit(ILSVarOperand node) => _variableProvider.GetValue(node.Parameters.ToArray());
    public virtual float Visit(ILSBinaryOperand<float> node) => ILSNumericOperand<float>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public virtual float Visit(ILSUnaryOperand<float> node) => ILSNumericOperand<float>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public virtual float Visit(ILSTernaryConditionalOperand<float> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public virtual int Visit(ILSConstantOperand<int> node) => node.Value;
    public virtual int Visit(ILSBinaryOperand<int> node) => ILSNumericOperand<int>.BinaryOperation(node.Operator, node.Left.Resolve(this), node.Right.Resolve(this));
    public virtual int Visit(ILSUnaryOperand<int> node) => ILSNumericOperand<int>.UnaryOperation(node.Operator, node.Operand.Resolve(this));
    public virtual int Visit(ILSTernaryConditionalOperand<int> node) => node.Condition.Resolve(this) ? node.TrueOperand.Resolve(this) : node.FalseOperand.Resolve(this);
    public virtual bool Visit(ILSConditionalOperand node) {
        var leftValue = node.Left.Resolve(this) as System.IComparable;
        var rightValue = node.Right.Resolve(this) as System.IComparable;
        if (leftValue == null || rightValue == null) throw new LSInvalidOperationException("Operands must be comparable.");
        return ILSBooleanOperand.ComparisonOperation(node.Operator, leftValue.CompareTo(rightValue));
    }
    public virtual bool Visit(ILSBinaryConditionalOperand node) => ILSBooleanOperand.BooleanOperation(node.Operator, node.Left, node.Right, this);
    public virtual bool Visit(ILSNegateBooleanOperand node) => !node.Operand.Resolve(this);
}
