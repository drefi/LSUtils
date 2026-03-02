namespace LSUtils.Tests.Core;

using System;
using System.Collections.Generic;
using NUnit.Framework;
[TestFixture]
public class LSOperandTests {
    LSOperandVisitor _evaluator;

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
    private class MockVarOperand : ILSVarOperand, ILSNumericOperand<float> {
        public string Id { get; }
        public string Key { get; }
        private object? _value;
        public MockVarOperand(string id, string key) {
            Id = id;
            Key = key;
        }
        public bool Accept(ILSOperandVisitor visitor, out float value, params object?[] parameters) {
            if (visitor.Visit(this, out value, Id, Key, parameters) == false) {
                value = default;
                return false;
            }

            SetValue(value, Id, Key, parameters);
            return true;
        }
        public bool Accept<TValue>(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters) {
            if (Accept(visitor, out var floatValue, parameters) == false || floatValue is not TValue castValue) {
                value = default;
                return false;
            }
            value = castValue;
            return true;
        }

        public bool Accept<TValue>(ILSVisitor visitor, out TValue? value, params object?[] parameters) {
            if (visitor is not ILSOperandVisitor operandVisitor) {
                value = default;
                return false;
            }
            return Accept(operandVisitor, out value, parameters);
        }

        public TValue? GetValue<TValue>(params object?[] parameters) {
            if (_value is TValue castValue) {
                return castValue;
            }
            throw new LSException($"Invalid type parameter. Expected {typeof(TValue).Name}, got {_value?.GetType().Name ?? "null"}.");
        }

        public void SetValue<TValue>(TValue value, params object?[] parameters) {
            _value = value;
        }

        public bool TryGetValue<TValue>(out TValue? value, params object?[] parameters) {
            if (_value is TValue castValue) {
                value = castValue;
                return true;
            }
            value = default;
            return false;
        }
    }
    private class MockVariableProvider : ILSVariableProvider {
        protected readonly Dictionary<string, MockEntity> _entities = new();
        public MockVariableProvider(MockEntity[]? entities = null) {
            if (entities != null) {
                foreach (var entity in entities) {
                    _entities[entity.Name] = entity;
                }
            }

        }

        public TValue? GetValue<TValue>(params object?[] parameters) {
            //if (parameters.Length != 2) throw new LSException($"Invalid number of parameters. Expected 2, got {parameters.Length}.");
            string id = parameters[0]?.ToString() ?? throw new LSException("Invalid id parameter.");
            string key = parameters[1]?.ToString() ?? throw new LSException("Invalid key parameter.");
            if (_entities.TryGetValue(id, out var entity)) {
                var attributeDefiner = _attributes[key];
                return (TValue)(object)entity.GetAttributeValue(attributeDefiner);
            }
            throw new LSException($"Variable '{id}' not found.");
        }

        public void SetValue<TValue>(TValue value, params object?[] parameters) {
            if (parameters.Length != 2) throw new LSException($"Invalid number of parameters. Expected 2, got {parameters.Length}.");
            string id = parameters[0]?.ToString() ?? throw new LSException("Invalid id parameter.");
            string key = parameters[1]?.ToString() ?? throw new LSException("Invalid key parameter.");
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
        public bool Accept(ILSOperandVisitor visitor, out float value, params object?[] parameters) {
            ResolveCount++;
            value = Value;
            return true;
        }

        public bool Accept<TValue>(ILSOperandVisitor visitor, out TValue? value, params object?[] parameters) {
            value = default;
            if (typeof(TValue) != typeof(float)) {
                throw new LSException($"Invalid type parameter. Expected float, got {typeof(TValue).Name}.");
            }
            if (Accept(visitor, out var floatValue, parameters) == false || floatValue is not TValue castValue) {
                throw new LSException("Failed to accept operand.");
            }
            value = castValue;
            return true;
        }

        public bool Accept<TValue>(ILSVisitor visitor, out TValue? value, params object?[] parameters) {
            return Accept(visitor as LSOperandVisitor ?? throw new LSException("Invalid visitor type."), out value, parameters);
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
        _evaluator = new LSOperandVisitor(_provider);
    }

    [Test]
    public void ConstantOperand_ShouldReturnValue() {
        var operand = new LSConstantOperand<float>(3.14f);
        operand.Accept(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(3.14f));
    }

    [Test]
    public void BinaryOperand_ShouldCalculateCorrectly() {
        var left = new LSConstantOperand<float>(2);
        var right = new LSConstantOperand<float>(3);
        var addOperand = new LSBinaryOperand<float>(left, right, MathOperator.Add);
        var mulOperand = new LSBinaryOperand<float>(left, right, MathOperator.Multiply);

        addOperand.Accept(_evaluator, out var addValue);
        mulOperand.Accept(_evaluator, out var mulValue);


        Assert.That(addValue, Is.EqualTo(5));
        Assert.That(mulValue, Is.EqualTo(6));
    }
    [Test]
    public void VarOperand_ShouldRetrieveValueFromProvider() {
        MockVariableProvider provider = new MockVariableProvider();
        var operand = new MockVarOperand("EntityA", "Health");
        operand.Accept<float>(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(100));
    }
    [Test]
    public void ComplexExpression_ShouldEvaluateCorrectly() {
        // (EntityA.Health + EntityB.Health) / 2
        var healthA = new MockVarOperand("EntityA", "Health");
        var healthB = new MockVarOperand("EntityB", "Health");
        var sum = new LSBinaryOperand<float>(healthA, healthB, MathOperator.Add);
        sum.Accept<float>(_evaluator, out var sumValue);
        Assert.That(sumValue, Is.EqualTo(250));
        var average = new LSBinaryOperand<float>(sum, new LSConstantOperand<float>(2), MathOperator.Divide);

        average.Accept<float>(_evaluator, out var averageValue);
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
        totalDamage.Accept<float>(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(80));
    }

    [Test]
    public void UnaryOperand_ShouldApplyNegate() {
        var operand = new LSUnaryOperand<float>(new LSConstantOperand<float>(5f), UnaryOperator.Negate);
        operand.Accept<float>(_evaluator, out var value);
        Assert.That(value, Is.EqualTo(-5f));
    }

    [Test]
    public void ConditionalOperand_ShouldCompareCorrectly() {
        var left = new LSConstantOperand<int>(5);
        var right = new LSConstantOperand<int>(3);
        var greaterThan = new LSConditionalOperand(ComparisonOperator.GreaterThan, left, right);
        greaterThan.Accept<bool>(_evaluator, out var gtValue);
        var lessThan = new LSConditionalOperand(ComparisonOperator.LessThan, left, right);
        lessThan.Accept<bool>(_evaluator, out var ltValue);

        Assert.That(gtValue, Is.True);
        Assert.That(ltValue, Is.False);
    }

    [Test]
    public void BinaryConditionalOperand_ShouldEvaluateAndOr() {
        var t = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(1));
        var f = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(2));

        var andOp = new LSBinaryConditionalOperand(BooleanOperator.And, t, f);
        var orOp = new LSBinaryConditionalOperand(BooleanOperator.Or, t, f);

        andOp.Accept<bool>(_evaluator, out var andValue);
        orOp.Accept<bool>(_evaluator, out var orValue);

        Assert.That(andValue, Is.False);
        Assert.That(orValue, Is.True);
    }

    [Test]
    public void GenericVisitorVisit_ShouldResolveBooleanOperand() {
        var condition = new LSConditionalOperand(ComparisonOperator.GreaterThan, new LSConstantOperand<int>(2), new LSConstantOperand<int>(1));

        var resolved = _evaluator.Visit<bool>(out var value, condition);

        Assert.That(resolved, Is.True);
        Assert.That(value, Is.True);
    }

    [Test]
    public void GenericVisitorVisit_ShouldResolveBooleanConstantOperand() {
        var constant = new LSBooleanConstantOperand(true);

        var resolved = _evaluator.Visit<bool>(out var value, constant);

        Assert.That(resolved, Is.True);
        Assert.That(value, Is.True);
    }

    [Test]
    public void TernaryConditionalOperand_ShouldSelectCorrectBranch() {
        var condition = new LSConditionalOperand(ComparisonOperator.GreaterThan, new LSConstantOperand<int>(2), new LSConstantOperand<int>(1));
        var trueBranch = new CountingOperand(10f);
        var falseBranch = new CountingOperand(20f);

        var ternary = new LSTernaryConditionalOperand<float>(condition, trueBranch, falseBranch);
        ternary.Accept<float>(_evaluator, out var result);

        Assert.That(result, Is.EqualTo(10f));
        Assert.That(trueBranch.ResolveCount, Is.EqualTo(1));
        Assert.That(falseBranch.ResolveCount, Is.EqualTo(0));
    }

    [Test]
    public void BinaryOperand_ShouldNotEvaluateOperandsTwice() {
        var left = new CountingOperand(2f);
        var right = new CountingOperand(3f);
        var add = new LSBinaryOperand<float>(left, right, MathOperator.Add);

        add.Accept<float>(_evaluator, out var result);

        Assert.That(result, Is.EqualTo(5f));
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
        newHealth.Accept<float>(_evaluator, out var result);
        Assert.That(result, Is.EqualTo(90));
        _entityA.SetAttribute((_attributes["Health"]), result);
        var updatedHealth = new MockVarOperand("EntityA", "Health");
        updatedHealth.Accept<float>(_evaluator, out var updatedResult);
        Assert.That(updatedResult, Is.EqualTo(90));
    }
    [Test]
    public void IntegrationTest_ShouldEvaluateAttributeChangeBetweenEntities() {
        // Simulate an attribute change: EntityA's new Health = CurrentHealth - EntityB's Damage
        var currentHealth = new MockVarOperand("EntityA", "Health");
        var damageFromB = new MockVarOperand("EntityB", "Damage");
        var newHealth = new LSBinaryOperand<float>(currentHealth, damageFromB, MathOperator.Subtract);
        newHealth.Accept<float>(_evaluator, out var result);
        Assert.That(result, Is.EqualTo(80));
        _entityA.SetAttribute((_attributes["Health"]), result);
        var updatedHealth = new MockVarOperand("EntityA", "Health");
        updatedHealth.Accept<float>(_evaluator, out var updatedResult);
        Assert.That(updatedResult, Is.EqualTo(80));
    }

}
