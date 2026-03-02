using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LSUtils;

public class LSOperandVisitor : ILSOperandVisitor {
    protected ILSVariableProvider _variableProvider;
    public LSOperandVisitor(ILSVariableProvider variableProvider) {
        _variableProvider = variableProvider;
    }

    public virtual bool Visit<TValue>(ILSVarOperand node, out TValue? value, params object?[] parameters) {
        value = _variableProvider.GetValue<TValue>(parameters);
        return value != null;
    }

    public virtual bool Visit<TValue>(ILSConstantOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : INumber<TValue> {
        value = node.Value;
        return true;
    }

    public virtual bool Visit<TValue>(ILSBinaryOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : INumber<TValue> {
        if (node.Left.Accept(this, out var leftValue, parameters) == false || leftValue == null) {
            value = default;
            return false;
        }
        if (node.Right.Accept(this, out var rightValue, parameters) == false || rightValue == null) {
            value = default;
            return false;
        }
        value = ILSNumericOperand<TValue>.BinaryOperation(node.Operator, leftValue, rightValue);
        return true;
    }

    public virtual bool Visit<TValue>(ILSUnaryOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : INumber<TValue> {
        if (node.Operand.Accept(this, out var operandValue, parameters) == false || operandValue == null) {
            value = default;
            return false;
        }
        value = ILSNumericOperand<TValue>.UnaryOperation(node.Operator, operandValue);
        return true;
    }

    public virtual bool Visit<TValue>(ILSTernaryConditionalOperand<TValue> node, out TValue? value, params object?[] parameters) where TValue : INumber<TValue> {
        if (node.Condition.Accept(this, out var conditionValue, parameters) == false) {
            value = default;
            return false;
        }
        if (conditionValue == true) {
            if (node.TrueOperand.Accept(this, out var trueValue, parameters) == false || trueValue == null) {
                value = default;
                return false;
            }
            value = trueValue;
            return true;
        } else {
            if (node.FalseOperand.Accept(this, out var falseValue, parameters) == false || falseValue == null) {
                value = default;
                return false;
            }
            value = falseValue;
            return true;
        }
    }

    public virtual bool Visit(ILSBooleanOperand node, out bool? value, params object?[] parameters) {
        return node switch {
            ILSComparerOperand comparerOperand => Visit(comparerOperand, out value, parameters),
            ILSConditionalOperand conditionalOperand => Visit(conditionalOperand, out value, parameters),
            ILSNegateBooleanOperand negateOperand => Visit(negateOperand, out value, parameters),
            LSBooleanConstantOperand constantOperand => (value = constantOperand.Value) != null,
            _ => (value = node.Value) != null
        };
    }

    public virtual bool Visit(ILSComparerOperand node, out bool? value, params object?[] parameters) {
        if (node.Left.Accept<System.IComparable>(this, out var leftValue, parameters) == false || leftValue == null) {
            value = default;
            return false;
        }
        if (node.Right.Accept<System.IComparable>(this, out var rightValue, parameters) == false || rightValue == null) {
            value = default;
            return false;
        }
        value = ILSBooleanOperand.ComparisonOperation(node.Operator, leftValue.CompareTo(rightValue));
        return true;
    }

    public virtual bool Visit(ILSConditionalOperand node, out bool? value, params object?[] parameters) {
        if (node.Left.Accept(this, out var leftValue, parameters) == false) {
            value = default;
            return false;
        }
        if (node.Right.Accept(this, out var rightValue, parameters) == false) {
            value = default;
            return false;
        }
        value = ILSBooleanOperand.BooleanOperation(node.Operator, node.Left, node.Right, this);
        return true;
    }

    public virtual bool Visit(ILSNegateBooleanOperand node, out bool? value, params object?[] parameters) {
        if (node.Operand.Accept(this, out var operandValue, parameters) == false) {
            value = default;
            return false;
        }
        value = !operandValue;
        return true;
    }

    public virtual bool Visit<TValue>(out TValue? value, params object?[] parameters) {
        Queue<object?> parameterQueue = new Queue<object?>(parameters);
        while (parameterQueue.Count > 0) {
            var parameter = parameterQueue.Dequeue();
            if (parameter is ILSOperand operand) {
                if (operand.Accept(this, out value, parameterQueue.ToArray())) {
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
