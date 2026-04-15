namespace LSUtils.OperandTree;

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
public abstract class StandardOperandVisitor : ILSOperandVisitor {
    protected ILSValueProvider? _valueProvider;
    public StandardOperandVisitor(ILSValueProvider? valueProvider = null) {
        _valueProvider = valueProvider;
    }

    public virtual bool Visit(ILSNumericOperand node, out object? value, params object?[] args) {
        if (node is ILSNumericOperand<float> floatNode) {
            if (Visit(floatNode, out var floatValue, args)) {
                value = floatValue;
                return true;
            }
        } else if (node is ILSNumericOperand<int> intNode) {
            if (Visit(intNode, out var intValue, args)) {
                value = intValue;
                return true;
            }
        } else if (node is ILSNumericOperand<double> doubleNode) {
            if (Visit(doubleNode, out var doubleValue, args)) {
                value = doubleValue;
                return true;
            }
        }
        value = null;
        return false;
    }

    public virtual bool Visit<TValue>(ILSNumericOperand<TValue> node, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        return node switch {
            ILSConstantOperand<TValue> constantOperand => Visit(constantOperand, out result, args),
            ILSBinaryOperand<TValue> binaryOperand => Visit(binaryOperand, out result, args),
            ILSUnaryOperand<TValue> unaryOperand => Visit(unaryOperand, out result, args),
            ILSTernaryConditionalOperand<TValue> ternaryOperand => Visit(ternaryOperand, out result, args),
            _ => (result = _valueProvider == null ? default : _valueProvider.GetValue<TValue>(args)) != null
        };
    }

    public virtual bool Visit<TValue>(ILSConstantOperand<TValue> node, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        result = node.Value;
        return true;
    }

    public virtual bool Visit<TValue>(ILSBinaryOperand<TValue> node, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (node.Left.Evaluate(this, out var leftValue, args) == false || leftValue == null) {
            result = default;
            return false;
        }
        if (node.Right.Evaluate(this, out var rightValue, args) == false || rightValue == null) {
            result = default;
            return false;
        }
        result = leftValue.BinaryOperation(node.Operator, rightValue);
        return true;
    }

    public virtual bool Visit<TValue>(ILSUnaryOperand<TValue> node, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (node.Operand.Evaluate(this, out var operandValue, args) == false || operandValue == null) {
            result = default;
            return false;
        }
        result = operandValue.UnaryOperation(node.Operator);
        return true;
    }

    public virtual bool Visit<TValue>(ILSTernaryConditionalOperand<TValue> node, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
        if (node.Condition.Evaluate(this, out var conditionValue, args) == false) {
            result = default;
            return false;
        }
        if (conditionValue == true) {
            if (node.TrueOperand.Evaluate(this, out var trueValue, args) == false || trueValue == null) {
                result = default;
                return false;
            }
            result = trueValue;
            return true;
        } else {
            if (node.FalseOperand.Evaluate(this, out var falseValue, args) == false || falseValue == null) {
                result = default;
                return false;
            }
            result = falseValue;
            return true;
        }
    }

    public virtual bool Visit(ILSBooleanOperand node, out bool? result, params object?[] args) {
        return node switch {
            ILSComparerOperand comparerOperand => Visit(comparerOperand, out result, args),
            ILSConditionalOperand conditionalOperand => Visit(conditionalOperand, out result, args),
            ILSNegateBooleanOperand negateOperand => Visit(negateOperand, out result, args),
            _ => (result = node.Value) != null
        };
    }

    public virtual bool Visit(ILSComparerOperand node, out bool? result, params object?[] args) {
        result = null;
        if (Visit(node.Left, out var leftResult, args) == false) {
            return false;
        }
        if (Visit(node.Right, out var rightResult, args) == false) {
            return false;
        }
        if (leftResult is System.IComparable leftComparable && rightResult is System.IComparable rightComparable) {
            result = leftComparable.Compare(rightComparable, node.Operator);
            return true;
        }
        return false;
    }

    public virtual bool Visit(ILSConditionalOperand node, out bool? result, params object?[] args) {
        if (node.Left.Evaluate(this, out var leftValue, args) == false || leftValue == null) {
            result = default;
            return false;
        }
        if (node.Right.Evaluate(this, out var rightValue, args) && rightValue.HasValue) {
            result = leftValue.Value.BinaryOperation(rightValue.Value, node.Operator);
            return true;
        } else {
            result = leftValue.Value.BinaryOperation(false, node.Operator);
        }
        return true;
    }

    public virtual bool Visit(ILSNegateBooleanOperand node, out bool? result, params object?[] args) {
        if (node.Operand.Evaluate(this, out var operandValue, args) == false || operandValue == null) {
            result = default;
            return false;
        }
        result = !operandValue.Value;
        return true;
    }

    public virtual bool Visit(params object?[] args) {
        var argList = new List<object?>(args);
        if (argList.OfType<ILSOperand>().FirstOrDefault() is not ILSOperand operand) {
            return false;
        }
        argList.Remove(operand);
        return operand switch {
            ILSNumericOperand numericOperand => Visit(numericOperand, out _, argList.ToArray()),
            ILSBooleanOperand booleanOperand => Visit(booleanOperand, out _, argList.ToArray()),
            _ => false
        };
    }
}
