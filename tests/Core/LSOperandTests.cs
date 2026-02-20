namespace LSUtils.Tests.Core;

using System;
using System.Collections.Generic;
using NUnit.Framework;
[TestFixture]
public class LSOperandTests {
    LSEvaluator _evaluator;

    private MockVariableProvider _provider;
    private MockEntity _entityA;
    private MockEntity _entityB;

    private static readonly Dictionary<string, MockAttributeDefiner> _attributes = new Dictionary<string, MockAttributeDefiner> {
        { "Health", new MockAttributeDefiner("Health") },
        { "Damage", new MockAttributeDefiner("Damage") }
    };
    #region Mock Classes
    public class MockAttributeDefiner {
        public string Name { get; }
        public MockAttributeDefiner(string name) {
            Name = name;
        }
    }
    public class MockAttribute {
        public MockAttributeDefiner Definer { get; }
        public float Value { get; set; }
        public MockAttribute(MockAttributeDefiner definer, float value) {
            Definer = definer;
            Value = value;
        }
    }
    public class MockEntity {
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

    public class MockVariableProvider : ILSVariableProvider {
        protected readonly Dictionary<string, MockEntity> _entities = new();
        public MockVariableProvider(MockEntity[]? entities = null) {
            if (entities != null) {
                foreach (var entity in entities) {
                    _entities[entity.Name] = entity;
                }
            }

        }
        public object? GetValue(params object?[] parameters) {
            if (parameters.Length != 2) {
                throw new LSException($"Invalid number of parameters. Expected 2, got {parameters.Length}.");
            }
            string id = parameters[0]?.ToString() ?? throw new LSException("Invalid id parameter.");
            string key = parameters[1]?.ToString() ?? throw new LSException("Invalid key parameter.");
            if (_entities.TryGetValue(id, out var value)) {
                return value.GetAttributeValue(_attributes[key]);
            }
            throw new LSException($"Variable '{id}' not found.");
        }
    }
    public class CountingOperand : ILSNumericOperand<float> {
        public float Value { get; }
        public int ResolveCount { get; private set; }

        public CountingOperand(float value) {
            Value = value;
        }

        public float Resolve(ILSOperandVisitor visitor) {
            ResolveCount++;
            return Value;
        }

        TOperand ILSOperand.Resolve<TOperand>(ILSOperandVisitor visitor) => (TOperand)(object)Resolve(visitor);

        object? ILSOperand.Resolve(ILSOperandVisitor visitor) {
            return Resolve(visitor);
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
        _evaluator = new LSEvaluator(_provider);
    }

    [Test]
    public void ConstantOperand_ShouldReturnValue() {
        var operand = new LSConstantOperand<float>(3.14f);
        Assert.That(operand.Resolve(_evaluator), Is.EqualTo(3.14f));
    }

    [Test]
    public void BinaryOperand_ShouldCalculateCorrectly() {
        var left = new LSConstantOperand<float>(2);
        var right = new LSConstantOperand<float>(3);
        var addOperand = new LSBinaryOperand<float>(left, right, MathOperator.Add);
        var mulOperand = new LSBinaryOperand<float>(left, right, MathOperator.Multiply);

        Assert.That(addOperand.Resolve(_evaluator), Is.EqualTo(5));
        Assert.That(mulOperand.Resolve(_evaluator), Is.EqualTo(6));
    }
    [Test]
    public void VarOperand_ShouldRetrieveValueFromProvider() {
        var operand = new LSVarOperand("EntityA", "Health");
        Assert.That(operand.Resolve(_evaluator), Is.EqualTo(100));
    }
    [Test]
    public void ComplexExpression_ShouldEvaluateCorrectly() {
        // (EntityA.Health + EntityB.Health) / 2
        var healthA = new LSNumericVarOperand<float>("EntityA", "Health");
        var healthB = new LSNumericVarOperand<float>("EntityB", "Health");
        var sum = new LSBinaryOperand<float>(healthA, healthB, MathOperator.Add);
        var average = new LSBinaryOperand<float>(sum, new LSConstantOperand<float>(2), MathOperator.Divide);

        Assert.That(average.Resolve(_evaluator), Is.EqualTo(125));
    }
    [Test]
    public void Visitor_ShouldEvaluateNestedExpressions() {
        // (EntityA.Damage * 2) + (EntityB.Damage * 3)
        var damageA = new LSNumericVarOperand<float>("EntityA", "Damage");
        var damageB = new LSNumericVarOperand<float>("EntityB", "Damage");
        var doubleDamageA = new LSBinaryOperand<float>(damageA, new LSConstantOperand<float>(2), MathOperator.Multiply);
        var tripleDamageB = new LSBinaryOperand<float>(damageB, new LSConstantOperand<float>(3), MathOperator.Multiply);
        var totalDamage = new LSBinaryOperand<float>(doubleDamageA, tripleDamageB, MathOperator.Add);

        Assert.That(totalDamage.Resolve(_evaluator), Is.EqualTo(80));
    }

    [Test]
    public void UnaryOperand_ShouldApplyNegate() {
        var operand = new LSUnaryOperand<float>(new LSConstantOperand<float>(5f), UnaryOperator.Negate);
        Assert.That(operand.Resolve(_evaluator), Is.EqualTo(-5f));
    }

    [Test]
    public void ConditionalOperand_ShouldCompareCorrectly() {
        var left = new LSConstantOperand<int>(5);
        var right = new LSConstantOperand<int>(3);
        var greaterThan = new LSConditionalOperand(ComparisonOperator.GreaterThan, left, right);
        var lessThan = new LSConditionalOperand(ComparisonOperator.LessThan, left, right);

        Assert.That(greaterThan.Resolve(_evaluator), Is.True);
        Assert.That(lessThan.Resolve(_evaluator), Is.False);
    }

    [Test]
    public void BinaryConditionalOperand_ShouldEvaluateAndOr() {
        var t = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(1));
        var f = new LSConditionalOperand(ComparisonOperator.Equal, new LSConstantOperand<int>(1), new LSConstantOperand<int>(2));

        var andOp = new LSBinaryConditionalOperand(BooleanOperator.And, t, f);
        var orOp = new LSBinaryConditionalOperand(BooleanOperator.Or, t, f);

        Assert.That(andOp.Resolve(_evaluator), Is.False);
        Assert.That(orOp.Resolve(_evaluator), Is.True);
    }

    [Test]
    public void TernaryConditionalOperand_ShouldSelectCorrectBranch() {
        var condition = new LSConditionalOperand(ComparisonOperator.GreaterThan, new LSConstantOperand<int>(2), new LSConstantOperand<int>(1));
        var trueBranch = new CountingOperand(10f);
        var falseBranch = new CountingOperand(20f);

        var ternary = new LSTernaryConditionalOperand<float>(condition, trueBranch, falseBranch);
        var result = ternary.Resolve(_evaluator);

        Assert.That(result, Is.EqualTo(10f));
        Assert.That(trueBranch.ResolveCount, Is.EqualTo(1));
        Assert.That(falseBranch.ResolveCount, Is.EqualTo(0));
    }

    [Test]
    public void BinaryOperand_ShouldNotEvaluateOperandsTwice() {
        var left = new CountingOperand(2f);
        var right = new CountingOperand(3f);
        var add = new LSBinaryOperand<float>(left, right, MathOperator.Add);

        var result = add.Resolve(_evaluator);

        Assert.That(result, Is.EqualTo(5f));
        Assert.That(left.ResolveCount, Is.EqualTo(1), "Left operand evaluated more than once.");
        Assert.That(right.ResolveCount, Is.EqualTo(1), "Right operand evaluated more than once.");
    }
    //Don't test exceptions or functionalities for classes that are implemented in the Test file (MockEntity, MockVariableProvider, etc), as they are not part of the actual codebase and are only used for testing purposes.

    //integration tests
    [Test]
    public void IntegrationTest_ShouldEvaluateAttributeChange() {
        // Simulate an attribute change: NewHealth = CurrentHealth - Damage
        var currentHealth = new LSNumericVarOperand<float>("EntityA", "Health");
        var damage = new LSNumericVarOperand<float>("EntityA", "Damage");
        var newHealth = new LSBinaryOperand<float>(currentHealth, damage, MathOperator.Subtract);
        var result = newHealth.Resolve(_evaluator);
        Assert.That(result, Is.EqualTo(90));
        _entityA.SetAttribute((_attributes["Health"]), result);
        var updatedHealth = new LSNumericVarOperand<float>("EntityA", "Health");
        Assert.That(updatedHealth.Resolve(_evaluator), Is.EqualTo(90));
    }
    [Test]
    public void IntegrationTest_ShouldEvaluateAttributeChangeBetweenEntities() {
        // Simulate an attribute change: EntityA's new Health = CurrentHealth - EntityB's Damage
        var currentHealth = new LSNumericVarOperand<float>("EntityA", "Health");
        var damageFromB = new LSNumericVarOperand<float>("EntityB", "Damage");
        var newHealth = new LSBinaryOperand<float>(currentHealth, damageFromB, MathOperator.Subtract);
        var result = newHealth.Resolve(_evaluator);
        Assert.That(result, Is.EqualTo(80));
        _entityA.SetAttribute((_attributes["Health"]), result);
        var updatedHealth = new LSNumericVarOperand<float>("EntityA", "Health");
        Assert.That(updatedHealth.Resolve(_evaluator), Is.EqualTo(80));
    }

}
