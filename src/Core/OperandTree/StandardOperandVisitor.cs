namespace LSUtils.OperandTree;

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
public abstract class StandardOperandVisitor : ILSOperandVisitor {
    protected ILSValueProvider? _valueProvider;
    public StandardOperandVisitor(ILSValueProvider? valueProvider = null) {
        _valueProvider = valueProvider;
    }

    public virtual bool Visit<TValue>(ILSNumericOperand<TValue> node, out TValue? value, params object?[] args) where TValue : INumber<TValue> {
        return node switch {
            ILSConstantOperand<TValue> attributeOperand => Visit(attributeOperand, out value, args),
            ILSBinaryOperand<TValue> binaryOperand => Visit(binaryOperand, out value, args),
            ILSUnaryOperand<TValue> unaryOperand => Visit(unaryOperand, out value, args),
            ILSTernaryConditionalOperand<TValue> ternaryOperand => Visit(ternaryOperand, out value, args),
            _ => (value = _valueProvider == null ? default : _valueProvider.GetValue<TValue>(args)) != null
        };
    }

    public virtual bool Visit<TValue>(ILSConstantOperand<TValue> node, out TValue? value, params object?[] args) where TValue : INumber<TValue> {
        value = node.Value;
        return true;
    }

    public virtual bool Visit<TValue>(ILSBinaryOperand<TValue> node, out TValue? value, params object?[] args) where TValue : INumber<TValue> {
        if (node.Left.Evaluate(this, out var leftValue, args) == false || leftValue == null) {
            value = default;
            return false;
        }
        if (node.Right.Evaluate(this, out var rightValue, args) == false || rightValue == null) {
            value = default;
            return false;
        }
        value = leftValue.BinaryOperation(node.Operator, rightValue);
        return true;
    }

    public virtual bool Visit<TValue>(ILSUnaryOperand<TValue> node, out TValue? value, params object?[] args) where TValue : INumber<TValue> {
        if (node.Operand.Evaluate(this, out var operandValue, args) == false || operandValue == null) {
            value = default;
            return false;
        }
        value = operandValue.UnaryOperation(node.Operator);
        return true;
    }

    public virtual bool Visit<TValue>(ILSTernaryConditionalOperand<TValue> node, out TValue? value, params object?[] args) where TValue : INumber<TValue> {
        if (node.Condition.Evaluate(this, out var conditionValue, args) == false) {
            value = default;
            return false;
        }
        if (conditionValue == true) {
            if (node.TrueOperand.Evaluate(this, out var trueValue, args) == false || trueValue == null) {
                value = default;
                return false;
            }
            value = trueValue;
            return true;
        } else {
            if (node.FalseOperand.Evaluate(this, out var falseValue, args) == false || falseValue == null) {
                value = default;
                return false;
            }
            value = falseValue;
            return true;
        }
    }

    public virtual bool Visit(ILSBooleanOperand node, out bool? value, params object?[] args) {
        return node switch {
            ILSComparerOperand comparerOperand => Visit(comparerOperand, out value, args),
            ILSConditionalOperand conditionalOperand => Visit(conditionalOperand, out value, args),
            ILSNegateBooleanOperand negateOperand => Visit(negateOperand, out value, args),
            _ => (value = node.Value) != null
        };
    }

    public virtual bool Visit(ILSComparerOperand node, out bool? value, params object?[] args) {
        if (node.Left.Evaluate<System.IComparable>(this, out var leftValue, args) == false || leftValue == null) {
            value = default;
            return false;
        }
        if (node.Right.Evaluate<System.IComparable>(this, out var rightValue, args) == false || rightValue == null) {
            value = default;
            return false;
        }
        value = leftValue.Compare(rightValue, node.Operator);
        return true;
    }

    public virtual bool Visit(ILSConditionalOperand node, out bool? value, params object?[] args) {
        if (node.Left.Evaluate(this, out var leftValue, args) == false || leftValue == null) {
            value = default;
            return false;
        }
        if (node.Right.Evaluate(this, out var rightValue, args) && rightValue.HasValue) {
            value = leftValue.Value.BinaryOperation(rightValue.Value, node.Operator);
            return true;
        } else {
            value = leftValue.Value.BinaryOperation(false, node.Operator);
        }
        return true;
    }

    public virtual bool Visit(ILSNegateBooleanOperand node, out bool? value, params object?[] args) {
        if (node.Operand.Evaluate(this, out var operandValue, args) == false || operandValue == null) {
            value = default;
            return false;
        }
        value = !operandValue.Value;
        return true;
    }

    public virtual bool Visit<TValue>(out TValue? value, params object?[] args) {
        Queue<object?> parameterQueue = new Queue<object?>(args);
        while (parameterQueue.Count > 0) {
            var parameter = parameterQueue.Dequeue();
            if (parameter is ILSOperand operand) {
                if (operand.Evaluate(this, out value, parameterQueue.ToArray())) {
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
