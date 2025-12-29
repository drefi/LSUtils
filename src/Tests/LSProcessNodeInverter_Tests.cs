namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using LSUtils.Logging;

/// <summary>
/// Tests for LSProcessNodeInverter functionality.
/// </summary>
[TestFixture]
public class NodeInverterTests {
    public class MockProcess : LSProcess {
        public bool ShouldInvert { get; set; } = true;
        public MockProcess() { }
    }

    private LSProcessHandler _successHandler = null!;
    private LSProcessHandler _failureHandler = null!;
    private LSProcessHandler _waitingHandler = null!;
    private LSProcessHandler _cancelledHandler = null!;
    private LSProcessHandler _unknownHandler = null!;
    private int _handlerCallCount;
    private LSLogger _logger = null!;

    [SetUp]
    public void Setup() {
        _logger = LSLogger.Singleton;
        _logger.ClearProviders();
        _logger.AddProvider(new LSConsoleLogProvider());
        LSProcessManager.DebugLogging();
        _handlerCallCount = 0;

        _successHandler = (session) => {
            _handlerCallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _failureHandler = (session) => {
            _handlerCallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _waitingHandler = (session) => {
            _handlerCallCount++;
            return LSProcessResultStatus.WAITING;
        };

        _cancelledHandler = (session) => {
            _handlerCallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _unknownHandler = (session) => {
            _handlerCallCount++;
            return LSProcessResultStatus.UNKNOWN;
        };
    }

    [TearDown]
    public void Cleanup() {
        _handlerCallCount = 0;
    }

    #region Basic Inversion Tests

    [Test]
    public void TestInverterNodeType() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("root", inv => inv
                .Handler("child", _successHandler))
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeType, Is.EqualTo(LSProcessLayerNodeType.INVERTER));
        Assert.That(builder.NodeID, Is.EqualTo("root"));
    }

    [Test]
    public void TestInverterSuccessToFailure() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("success-handler", _successHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // SUCCESS should be inverted to FAILURE
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterFailureToSuccess() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("failure-handler", _failureHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // FAILURE should be inverted to SUCCESS
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterWaitingPassThrough() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("waiting-handler", _waitingHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // WAITING should pass through unchanged
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterCancelledPassThrough() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("cancelled-handler", _cancelledHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // CANCELLED should pass through unchanged
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterUnknownPassThrough() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("unknown-handler", _unknownHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // UNKNOWN should pass through unchanged
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.UNKNOWN));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    #endregion

    #region Generic Type-Safe Tests

    [Test]
    public void TestInverterGenericTypeSafe() {
        var builder = new LSProcessTreeBuilder()
            .Inverter<MockProcess>("inverter", inv => inv
                .Handler<MockProcess>("typed-handler", session => {
                    // Type-safe access to MockProcess properties
                    var shouldInvert = session.Process.ShouldInvert;
                    return shouldInvert ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE;
                }))
            .Build();

        var mockProcess = new MockProcess { ShouldInvert = true };
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // SUCCESS inverted to FAILURE
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void TestInverterGenericWithCondition() {
        var builder = new LSProcessTreeBuilder()
            .Inverter<MockProcess>("inverter", inv => inv
                .Handler<MockProcess>("conditional-handler", session => 
                    LSProcessResultStatus.SUCCESS),
                conditions: process => process.ShouldInvert)
            .Build();

        // Test with condition true
        var mockProcess1 = new MockProcess { ShouldInvert = true };
        var session1 = new LSProcessSession(null!, mockProcess1, builder);
        var result1 = session1.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // SUCCESS inverted

        // Test with condition false
        var mockProcess2 = new MockProcess { ShouldInvert = false };
        var session2 = new LSProcessSession(null!, mockProcess2, builder);
        var result2 = session2.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.FAILURE)); // Condition not met
    }

    #endregion

    #region Condition Tests

    [Test]
    public void TestInverterWithConditionMet() {
        bool conditionMet = true;

        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("handler", _successHandler),
                conditions: process => conditionMet)
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // Condition met, child executes, SUCCESS inverted to FAILURE
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterWithConditionNotMet() {
        bool conditionMet = false;

        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("handler", _successHandler),
                conditions: process => conditionMet)
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // Condition not met, returns FAILURE without executing child
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handlerCallCount, Is.EqualTo(0)); // Child not executed
    }

    #endregion

    #region Nested Inverter Tests

    [Test]
    public void TestDoubleInverterCancelsOut() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("outer", outer => outer
                .Inverter("inner", inner => inner
                    .Handler("handler", _successHandler)))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // SUCCESS -> FAILURE (inner) -> SUCCESS (outer)
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handlerCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInverterInSequence() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("pre", _successHandler)
                .Inverter("inverter", inv => inv
                    .Handler("inverted", _failureHandler)) // FAILURE -> SUCCESS
                .Handler("post", _successHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // All handlers succeed (inverter converts FAILURE to SUCCESS)
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handlerCallCount, Is.EqualTo(3));
    }

    [Test]
    public void TestInverterInSelector() {
        var builder = new LSProcessTreeBuilder()
            .Selector("root", sel => sel
                .Inverter("inverter", inv => inv
                    .Handler("inverted", _successHandler)) // SUCCESS -> FAILURE
                .Handler("fallback", _successHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // First child (inverter) fails, selector tries fallback which succeeds
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handlerCallCount, Is.EqualTo(2)); // Both handlers executed
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void TestInverterWithParallelChild() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Parallel("parallel", par => par
                    .Handler("h1", _successHandler)
                    .Handler("h2", _successHandler),
                    numRequiredToSucceed: 2))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // Parallel succeeds (2/2), inverted to FAILURE
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handlerCallCount, Is.EqualTo(2));
    }

    [Test]
    public void TestInverterWithSequenceChild() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Sequence("sequence", seq => seq
                    .Handler("h1", _successHandler)
                    .Handler("h2", _failureHandler))) // Sequence fails at h2
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // Sequence fails, inverted to SUCCESS
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handlerCallCount, Is.EqualTo(2));
    }

    [Test]
    public void TestInverterForNegativeValidation() {
        // Real-world scenario: Reject invalid data
        LSProcessHandler<MockProcess> strictValidator = session => {
            // Strict validation that returns SUCCESS if valid
            return session.Process.ShouldInvert 
                ? LSProcessResultStatus.SUCCESS 
                : LSProcessResultStatus.FAILURE;
        };

        // Test with valid data (should be rejected)
        var builder1 = new LSProcessTreeBuilder()
            .Sequence("validation-flow", seq => seq
                .Inverter<MockProcess>("reject-if-valid", inv => inv
                    .Handler<MockProcess>("validate", strictValidator))
                .Handler("process-invalid", session => LSProcessResultStatus.SUCCESS))
            .Build();

        var validProcess = new MockProcess { ShouldInvert = true };
        var session1 = new LSProcessSession(null!, validProcess, builder1);
        var result1 = session1.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // Valid -> rejected

        // Test with invalid data (should proceed) - NEW BUILDER
        var builder2 = new LSProcessTreeBuilder()
            .Sequence("validation-flow", seq => seq
                .Inverter<MockProcess>("reject-if-valid", inv => inv
                    .Handler<MockProcess>("validate", strictValidator))
                .Handler("process-invalid", session => LSProcessResultStatus.SUCCESS))
            .Build();

        var invalidProcess = new MockProcess { ShouldInvert = false };
        var session2 = new LSProcessSession(null!, invalidProcess, builder2);
        var result2 = session2.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // Invalid -> processed
    }

    #endregion

    #region Error Cases

    [Test]
    public void TestInverterWithNoChild() {
        // This should throw during build or create warning
        var inverterNode = LSProcessNodeInverter.Create("empty", LSProcessPriority.NORMAL, 0, false);
        
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, inverterNode);
        
        // Execute with no child should return UNKNOWN and log warning
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.UNKNOWN));
    }

    [Test]
    public void TestInverterSingleChildConstraint() {
        var inverterNode = LSProcessNodeInverter.Create("inverter", LSProcessPriority.NORMAL, 0, false);
        var handler1 = LSProcessNodeHandler.Create("h1", _successHandler, 0);
        var handler2 = LSProcessNodeHandler.Create("h2", _successHandler, 1);
        
        // First child should succeed
        Assert.DoesNotThrow(() => inverterNode.AddChild(handler1));
        
        // Second child should throw
        var ex = Assert.Throws<LSException>(() => inverterNode.AddChild(handler2));
        Assert.That(ex?.Message, Does.Contain("already has a child"));
    }

    [Test]
    public void TestInverterReadOnly() {
        var inverterNode = LSProcessNodeInverter.Create("inverter", 
            LSProcessPriority.NORMAL, 0, readOnly: true);
        var handler = LSProcessNodeHandler.Create("h1", _successHandler, 0);
        
        // Adding child to read-only inverter should throw
        var ex = Assert.Throws<LSException>(() => inverterNode.AddChild(handler));
        Assert.That(ex?.Message, Does.Contain("read-only"));
    }

    #endregion

    #region Resume/Fail/Cancel Tests

    [Test]
    public void TestInverterResumeWaitingChild() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("waiting-handler", _waitingHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));

        // Resume forces child to SUCCESS, inverted to FAILURE
        mockProcess.SetData("resumed", true);
        var resumeResult = session.Resume("waiting-handler");
        
        // Child resumes to SUCCESS (default behavior), inverted to FAILURE
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void TestInverterFailWaitingChild() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("waiting-handler", _waitingHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));

        // Fail the waiting child
        var failResult = session.Fail("waiting-handler");
        
        // Child fails, inverted to SUCCESS
        Assert.That(failResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestInverterCancelChild() {
        var builder = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("waiting-handler", _waitingHandler))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));

        // Cancel should pass through
        session.Cancel();
        var status = builder.GetNodeStatus();
        Assert.That(status, Is.EqualTo(LSProcessResultStatus.CANCELLED));
    }

    #endregion

    #region Priority Tests

    [Test]
    public void TestInverterRespectsPriority() {
        List<string> executionOrder = new List<string>();

        var builder = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Inverter("low-priority", inv => inv
                    .Handler("low", session => {
                        executionOrder.Add("LOW");
                        return LSProcessResultStatus.SUCCESS;
                    }), priority: LSProcessPriority.LOW)
                .Inverter("high-priority", inv => inv
                    .Handler("high", session => {
                        executionOrder.Add("HIGH");
                        return LSProcessResultStatus.SUCCESS;
                    }), priority: LSProcessPriority.HIGH), numRequiredToSucceed: 2)
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        // Both fail (inverted SUCCESS), parallel fails  
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        
        // HIGH priority should execute first
        Assert.That(executionOrder.Count, Is.EqualTo(2));
        Assert.That(executionOrder[0], Is.EqualTo("HIGH"));
        Assert.That(executionOrder[1], Is.EqualTo("LOW"));
    }

    #endregion

    #region Clone Tests

    [Test]
    public void TestInverterClone() {
        var original = new LSProcessTreeBuilder()
            .Inverter("inverter", inv => inv
                .Handler("handler", _successHandler))
            .Build();

        var cloned = original.Clone();

        Assert.That(cloned.NodeID, Is.EqualTo(original.NodeID));
        Assert.That(cloned.NodeType, Is.EqualTo(original.NodeType));
        Assert.That(cloned.GetChildren().Length, Is.EqualTo(original.GetChildren().Length));

        // Execute both to ensure they're independent
        var mockProcess1 = new MockProcess();
        var session1 = new LSProcessSession(null!, mockProcess1, original);
        var result1 = session1.Execute();

        var mockProcess2 = new MockProcess();
        var session2 = new LSProcessSession(null!, mockProcess2, cloned);
        var result2 = session2.Execute();

        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(_handlerCallCount, Is.EqualTo(2)); // Both executed
    }

    #endregion
}
