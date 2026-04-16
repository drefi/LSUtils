namespace LSUtils.Tests.OperandTree;

using System;
using System.Collections.Generic;
using System.Numerics;
using LSUtils.OperandTree;
using NUnit.Framework;
[TestFixture]
public class LSOperandTests {
    StandardOperandVisitor _evaluator;

    private MockVariableProvider _provider;
    private MockEntity _entityA;
    private MockEntity _entityB;

    private static readonly Dictionary<string, MockAttributeDefiner> _attributes = new Dictionary<string, MockAttributeDefiner> {
        { "Health", new MockAttributeDefiner("Health") },
        { "Damage", new MockAttributeDefiner("Damage") }
    };
    #region Mock Classes
    private class MockAttributeDefiner {
        public string Name { get; }
        public MockAttributeDefiner(string name) {
            Name = name;
        }
    }
    private class MockOperandEvaluator : StandardOperandVisitor {
        public MockOperandEvaluator(ILSValueProvider? valueProvider = null) : base(valueProvider) { }
    }
    private class MockAttribute {
        public MockAttributeDefiner Definer { get; }
        public float Value { get; set; }
        public MockAttribute(MockAttributeDefiner definer, float value) {
            Definer = definer;
            Value = value;
        }
    }
    private class MockEntity {
        public string Name { get; }
        public Dictionary<MockAttributeDefiner, MockAttribute> Attributes { get; } = new();
        public MockEntity(string name = "Entity") {
            Name = name;
        }
        public void AddAttribute(MockAttributeDefiner definer, float initialValue) {
            Attributes[definer] = new MockAttribute(definer, initialValue);
        }
        public void SetAttribute(MockAttributeDefiner definer, float newValue) {
            if (Attributes.TryGetValue(definer, out var attribute)) {
                attribute.Value = newValue;
            } else {
                throw new LSException($"Attribute '{definer.Name}' not found in entity '{Name}'.");
            }
        }
        public float GetAttributeValue(MockAttributeDefiner definer) {
            if (Attributes.TryGetValue(definer, out var attribute)) {
                return attribute.Value;
            }
            throw new LSException($"Attribute '{definer.Name}' not found in entity '{Name}'.");
        }
    }
    private class MockVarOperand : ILSNumericOperand<float> {
        public string Id { get; }
        public string Key { get; }
        public MockVarOperand(string id, string key) {
            Id = id;
            Key = key;
        }
        public bool Evaluate(ILSOperandVisitor visitor, out float result, params object?[] args) {
            return visitor.Visit(this, out result, Id, Key, args);
        }


        public bool Accept(ILSVisitor visitor, params object?[] args) {
            return visitor.Visit(this, args);
        }

        public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
            if (Evaluate(visitor, out var floatValue, args) == false || floatValue is not TValue castValue) {
                result = default;
                return false;
            }
            result = castValue;
            return true;
        }
    }
    private class MockVariableProvider : ILSValueProvider {
        protected readonly Dictionary<string, MockEntity> _entities = new();
        public MockVariableProvider(MockEntity[]? entities = null) {
            if (entities != null) {
                foreach (var entity in entities) {
                    _entities[entity.Name] = entity;
                }
            }

        }

        public TValue? GetValue<TValue>(params object?[] args) {
            //if (parameters.Length != 2) throw new LSException($"Invalid number of parameters. Expected 2, got {parameters.Length}.");
            string id = args[0]?.ToString() ?? throw new LSException("Invalid id parameter.");
            string key = args[1]?.ToString() ?? throw new LSException("Invalid key parameter.");
            if (_entities.TryGetValue(id, out var entity)) {
                var attributeDefiner = _attributes[key];
                return (TValue)(object)entity.GetAttributeValue(attributeDefiner);
            }
            throw new LSException($"Variable '{id}' not found.");
        }

        public void SetValue<TValue>(TValue value, params object?[] args) {
            if (args.Length != 2) throw new LSException($"Invalid number of parameters. Expected 2, got {args.Length}.");
            string id = args[0]?.ToString() ?? throw new LSException("Invalid id parameter.");
            string key = args[1]?.ToString() ?? throw new LSException("Invalid key parameter.");
            if (_entities.TryGetValue(id, out var entity)) {
                var attributeDefiner = _attributes[key];
                entity.SetAttribute(attributeDefiner, Convert.ToSingle(value));
                return;
            }
            throw new LSException($"Variable '{id}' not found.");
        }

        public bool TryGetValue<TValue>(out TValue? value, params object?[] parameters) {
            try {
                value = GetValue<TValue>(parameters);
                return true;
            } catch {
                value = default;
                return false;
            }
        }
    }
    private class CountingOperand : ILSNumericOperand<float> {
        public float Value { get; }
        public int ResolveCount { get; private set; }

        public CountingOperand(float value) {
            Value = value;
        }
        public bool Evaluate(ILSOperandVisitor visitor, out float value, params object?[] args) {
            ResolveCount++;
            value = Value;
            return true;
        }

        public bool Accept(ILSVisitor visitor, params object?[] args) {
            return visitor.Visit(this, args);
        }

        public bool Evaluate<TValue>(ILSOperandVisitor visitor, out TValue? result, params object?[] args) where TValue : INumber<TValue> {
            if (Evaluate(visitor, out var floatValue, args) == false || floatValue is not TValue castValue) {
                result = default;
                return false;
            }
            result = castValue;
            return true;
        }
    }
    #endregion

    [SetUp]
    public void Setup() {
        _entityA = new MockEntity("EntityA");
        _entityA.AddAttribute(_attributes["Health"], 100);
        _entityA.AddAttribute(_attributes["Damage"], 10);
        _entityB = new MockEntity("EntityB");
        _entityB.AddAttribute(_attributes["Health"], 150);
        _entityB.AddAttribute(_attributes["Damage"], 20);
        _provider = new MockVariableProvider(new[] { _entityA, _entityB });
        _evaluator = new MockOperandEvaluator(_provider);
    }

    [Test]
    public void ConstantOperand_ShouldReturnValue() {
        var operand = new LSConstantOperand<float>(3.14f);
        operand.Evaluate(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(3.14f));
    }

    [Test]
    public void BinaryOperand_ShouldCalculateCorrectly() {
        var left = new LSConstantOperand<float>(2);
        var right = new LSConstantOperand<float>(3);
        var addOperand = new LSBinaryOperand<float>(left, right, MathOperator.Add);
        var mulOperand = new LSBinaryOperand<float>(left, right, MathOperator.Multiply);

        addOperand.Evaluate(_evaluator, out var addValue);
        mulOperand.Evaluate(_evaluator, out var mulValue);


        Assert.That(addValue, Is.EqualTo(5));
        Assert.That(mulValue, Is.EqualTo(6));
    }
    [Test]
    public void VarOperand_ShouldRetrieveValueFromProvider() {
        MockVariableProvider provider = new MockVariableProvider();
        var operand = new MockVarOperand("EntityA", "Health");
        operand.Evaluate<float>(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(100));
    }
    [Test]
    public void ComplexExpression_ShouldEvaluateCorrectly() {
        // (EntityA.Health + EntityB.Health) / 2
        var healthA = new MockVarOperand("EntityA", "Health");
        var healthB = new MockVarOperand("EntityB", "Health");
        var sum = new LSBinaryOperand<float>(healthA, healthB, MathOperator.Add);
        sum.Evaluate(_evaluator, out var sumValue);
        Assert.That(sumValue, Is.EqualTo(250));
        var average = new LSBinaryOperand<float>(sum, new LSConstantOperand<float>(2), MathOperator.Divide);

        average.Evaluate(_evaluator, out var averageValue);
        Assert.That(averageValue, Is.EqualTo(125));
    }
    [Test]
    public void Visitor_ShouldEvaluateNestedExpressions() {
        // (EntityA.Damage * 2) + (EntityB.Damage * 3)
        var damageA = new MockVarOperand("EntityA", "Damage");
        var damageB = new MockVarOperand("EntityB", "Damage");
        var doubleDamageA = new LSBinaryOperand<float>(damageA, new LSConstantOperand<float>(2), MathOperator.Multiply);
        var tripleDamageB = new LSBinaryOperand<float>(damageB, new LSConstantOperand<float>(3), MathOperator.Multiply);
        var totalDamage = new LSBinaryOperand<float>(doubleDamageA, tripleDamageB, MathOperator.Add);
        totalDamage.Evaluate(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(80));
    }

    [Test]
    public void UnaryOperand_ShouldApplyNegate() {
        var operand = new LSUnaryOperand<float>(new LSConstantOperand<float>(5f), UnaryOperator.Negate);
        operand.Evaluate(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(-5f));
    }

    [Test]
    public void ConditionalOperand_ShouldCompareCorrectly() {
        var left = new LSConstantOperand<int>(5);
        var right = new LSConstantOperand<int>(3);
        var greaterThan = new LSConditionalOperand(ComparisonOperator.GreaterThan, left, right);
        greaterThan.Evaluate(_evaluator, out var gtValue);
        var lessThan = new LSConditionalOperand(ComparisonOperator.LessThan, left, right);
        lessThan.Evaluate(_evaluator, out var ltValue);

        Assert.That(gtValue, Is.True);
        Assert.That(ltValue, Is.False);
    }

    [Test]
    public void BinaryConditionalOperand_ShouldEvaluateAndOr() {
        var t = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(1));
        var f = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(2));

        var andOp = new LSBinaryConditionalOperand(BooleanOperator.And, t, f);
        var orOp = new LSBinaryConditionalOperand(BooleanOperator.Or, t, f);

        andOp.Evaluate(_evaluator, out var andValue);
        orOp.Evaluate(_evaluator, out var orValue);

        Assert.That(andValue, Is.False);
        Assert.That(orValue, Is.True);
    }

    [Test]
    public void GenericVisitorVisit_ShouldResolveBooleanOperand() {
        var condition = new LSConditionalOperand(ComparisonOperator.GreaterThan, new LSConstantOperand<int>(2), new LSConstantOperand<int>(1));
        condition.Evaluate(_evaluator, out var conditionValue);
        Assert.That(conditionValue, Is.True);
    }

    [Test]
    public void GenericVisitorVisit_ShouldResolveBooleanConstantOperand() {
        var constant = new LSBooleanConstantOperand(true);

        constant.Evaluate(_evaluator, out var constantValue);
        Assert.That(constantValue, Is.True);
    }

    [Test]
    public void TernaryConditionalOperand_ShouldSelectCorrectBranch() {
        var condition = new LSConditionalOperand(ComparisonOperator.GreaterThan, new LSConstantOperand<int>(2), new LSConstantOperand<int>(1));
        var trueBranch = new CountingOperand(10f);
        var falseBranch = new CountingOperand(20f);

        var ternary = new LSTernaryConditionalOperand<float>(condition, trueBranch, falseBranch);
        ternary.Evaluate(_evaluator, out var result);

        Assert.That(result, Is.EqualTo(10f));
        Assert.That(trueBranch.ResolveCount, Is.EqualTo(1));
        Assert.That(falseBranch.ResolveCount, Is.EqualTo(0));
    }

    [Test]
    public void BinaryOperand_ShouldNotEvaluateOperandsTwice() {
        var left = new CountingOperand(2f);
        var right = new CountingOperand(3f);
        var add = new LSBinaryOperand<float>(left, right, MathOperator.Add);

        add.Evaluate(_evaluator, out var resultValue);

        Assert.That(resultValue, Is.EqualTo(5f));
        Assert.That(left.ResolveCount, Is.EqualTo(1), "Left operand evaluated more than once.");
        Assert.That(right.ResolveCount, Is.EqualTo(1), "Right operand evaluated more than once.");
    }
    //Don't test exceptions or functionalities for classes that are implemented in the Test file (MockEntity, MockVariableProvider, etc), as they are not part of the actual codebase and are only used for testing purposes.

    //integration tests
    [Test]
    public void IntegrationTest_ShouldEvaluateAttributeChange() {
        // Simulate an attribute change: NewHealth = CurrentHealth - Damage
        var currentHealth = new MockVarOperand("EntityA", "Health");
        var damage = new MockVarOperand("EntityA", "Damage");
        var newHealth = new LSBinaryOperand<float>(currentHealth, damage, MathOperator.Subtract);
        newHealth.Evaluate(_evaluator, out var resultValue);
        Assert.That(resultValue, Is.EqualTo(90));
        _entityA.SetAttribute((_attributes["Health"]), resultValue);
        var updatedHealth = new MockVarOperand("EntityA", "Health");
        updatedHealth.Evaluate<float>(_evaluator, out var updatedResult);
        Assert.That(updatedResult, Is.EqualTo(90));
    }
    [Test]
    public void IntegrationTest_ShouldEvaluateAttributeChangeBetweenEntities() {
        // Simulate an attribute change: EntityA's new Health = CurrentHealth - EntityB's Damage
        var currentHealth = new MockVarOperand("EntityA", "Health");
        var damageFromB = new MockVarOperand("EntityB", "Damage");
        var newHealth = new LSBinaryOperand<float>(currentHealth, damageFromB, MathOperator.Subtract);
        newHealth.Evaluate(_evaluator, out var resultValue);
        Assert.That(resultValue, Is.EqualTo(80));
        _entityA.SetAttribute((_attributes["Health"]), resultValue);
        var updatedHealth = new MockVarOperand("EntityA", "Health");
        updatedHealth.Evaluate<float>(_evaluator, out var updatedResult);
        Assert.That(updatedResult, Is.EqualTo(80));
    }

}
