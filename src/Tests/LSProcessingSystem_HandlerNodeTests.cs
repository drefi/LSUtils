using LSUtils.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.ProcessSystem.Tests;

/// <summary>
/// Tests for LSProcessNodeHandler functionality and conditions.
/// </summary>
[TestFixture]
public class LSProcessingSystem_HandlerNodeTests {
    public class MockProcess : LSProcess {
        public MockProcess() { }
    }

    private LSProcessHandler _mockHandler1;
    private LSProcessHandler _mockHandler2;
    private LSProcessHandler _mockHandler3Failure;
    private LSProcessHandler _mockHandler3Cancel;
    private LSProcessHandler _mockHandler3Waiting;
    private int _handler1CallCount;
    private int _handler2CallCount;
    private int _handler3CallCount;
    private LSLogger _logger;

    [SetUp]
    public void Setup() {
        _logger = LSLogger.Singleton;
        _logger.ClearProviders();
        _logger.AddProvider(new LSConsoleLogProvider());
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;

        _mockHandler1 = (session) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler2 = (session) => {
            _handler2CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler3Failure = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _mockHandler3Cancel = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _mockHandler3Waiting = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.WAITING;
        };
    }

    [TearDown]
    public void Cleanup() {
        // Reset for next test
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;
    }

    [Test]
    public void TestHandlerWithConditionsFalse() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.FAILURE;
                }, LSProcessPriority.NORMAL, (proc, node) => false)) // skip the handler
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, builder);

        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // since we skipped the handler that would return FAILURE the result is SUCCESS
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // handler should not be called because conditionMet == false
    }

    [Test]
    public void TestHandlerWithConditionsTrue() {
        var builder = new LSProcessTreeBuilder()
                    .Sequence("root", seq => seq
                        .Handler("conditionalHandler", (session) => {
                            _handler1CallCount++;
                            return LSProcessResultStatus.FAILURE;
                        }, LSProcessPriority.NORMAL, (proc, node) => true)) // condition met
                    .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, builder);

        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE)); // since the handler returns FAILURE the result is FAILURE
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler should be called because handler condition is true
    }

    [Test]
    public void TestLSProcessHandlerNodeExecutionCountSharing() {
        // Create original handler node
        var originalHandler = LSProcessNodeHandler.Create("handler", _mockHandler1, 0);

        // Create clone
        var cloneHandler = originalHandler.Clone() as LSProcessNodeHandler;
        Assert.That(cloneHandler, Is.Not.Null);

        // Both should start with execution count 0
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(0));
        Assert.That(cloneHandler!.ExecutionCount, Is.EqualTo(0));

        // Execute original
        var root = new LSProcessTreeBuilder().Sequence("root").Handler("handler", _mockHandler1).Build();
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, root);
        session.Execute();

        // Create a builder with the original handler to test execution count
        var testContext = new LSProcessSession(mockProcess, originalHandler);
        ((ILSProcessNode)originalHandler).Execute(testContext);

        // Both should show increased execution count
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(1));
        Assert.That(cloneHandler.ExecutionCount, Is.EqualTo(1));

        // Execute clone
        ((ILSProcessNode)cloneHandler).Execute(testContext);

        // Both should show further increased count
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(2));
        Assert.That(cloneHandler.ExecutionCount, Is.EqualTo(2));
    }

    [Test]
    public void TestLSProcessHandlerNodeBaseNodeReference() {
        var originalHandler = LSProcessNodeHandler.Create("handler", _mockHandler1, 0);
        var cloneHandler = originalHandler.Clone() as LSProcessNodeHandler;

        Assert.That(cloneHandler, Is.Not.Null);

        // Test that clone maintains reference while original doesn't
        // Note: We can't directly access _baseNode as it's protected, 
        // but we can test the behavior through execution count sharing
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, originalHandler);

        // Original execution count should be independent before cloning
        ((ILSProcessNode)originalHandler).Execute(session);
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(1));

        // Clone should share the count
        ((ILSProcessNode)cloneHandler!).Execute(session);
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(2));
        Assert.That(cloneHandler.ExecutionCount, Is.EqualTo(2));
    }

    [Test]
    public void TestMultipleConditionComposition() {
        int conditionCallCount = 0;
        bool eventTypeCondition = true;
        bool stateCondition = true;

        // Test case 1: Both conditions true - should execute
        var root = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL,
                   (proc, node) => { conditionCallCount++; return eventTypeCondition; },
                   (proc, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = true;
        stateCondition = true;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockProcess1 = new MockProcess();
        var session1 = new LSProcessSession(mockProcess1, root);
        var result1 = session1.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(conditionCallCount, Is.EqualTo(2)); // both conditions evaluated

        // Test case 2: First condition false - should not execute
        var root2 = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL,
                   (proc, node) => { conditionCallCount++; return eventTypeCondition; },
                   (proc, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = false;
        stateCondition = true;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockProcess2 = new MockProcess();
        var session2 = new LSProcessSession(mockProcess2, root2);
        var result2 = session2.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // sequence succeeds when handler is skipped
        Assert.That(_handler1CallCount, Is.EqualTo(0));
        Assert.That(conditionCallCount, Is.EqualTo(1)); // only first condition evaluated (short-circuit)

        // Test case 3: Second condition false - should not execute
        var root3 = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL,
                   (proc, node) => { conditionCallCount++; return eventTypeCondition; },
                   (proc, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = true;
        stateCondition = false;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockProcess3 = new MockProcess();
        var session3 = new LSProcessSession(mockProcess3, root3);
        var result3 = session3.Execute();
        Assert.That(result3, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0));
        Assert.That(conditionCallCount, Is.EqualTo(2)); // both conditions evaluated
    }

    [Test]
    public void TestConditionParameterValidation() {
        // Test with data-based conditions
        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("dataConditionHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, (proc, node) => {
                    return true;
                }))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, builder);
        var result = session.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // should execute because condition is met
    }

    [Test]
    public void TestConditionExceptionHandling() {
        bool throwException = true;

        var root = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("exceptionConditionHandler", (session) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, (proc, node) => {
                    if (throwException) throw new InvalidOperationException("Condition error");
                    return true;
                }))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, root);

        // Exception in condition should propagate (current implementation behavior)
        throwException = true;
        Assert.Throws<InvalidOperationException>(() => {
            session.Execute();
        });
    }
}
