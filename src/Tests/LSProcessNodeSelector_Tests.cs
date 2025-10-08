using LSUtils.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.ProcessSystem.Tests;

/// <summary>
/// Tests for LSProcessNodeSelector functionality.
/// </summary>
[TestFixture]
public class LSProcessNodeSelector_Tests {
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
    public void Execute_Empty() {
        var root = LSProcessNodeSelector.Create("root", 0);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        // Test execution

        var result = new LSProcessSession(new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Execute_Success() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution
        var result = new LSProcessSession(new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // should not be called because handler1 succeeds
    }
[Test]
    public void Execute_Success_2() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Failure, 0);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution
        var result = new LSProcessSession(new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // should be called because handler1 fails
    }
    [Test]
    public void Execute_Failure() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Failure, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler3Failure, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var result = new LSProcessSession(new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); // Both handlers should be called
    }

    [Test]
    public void Execute_WaitingResume() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler3Waiting, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution

        var session = new LSProcessSession(new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = session.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void Execute_WaitingFailResume() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler3Waiting, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution

        var session = new LSProcessSession(new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = session.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); // should call handler2 now
        // Resume handler2
        var resumeResult2 = session.Resume("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void Execute_WaitingFail() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler3Waiting, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution

        var session = new LSProcessSession(new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = session.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); // should call handler2 now
        // Fail handler2
        var resumeResult2 = session.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void Execute_WaitingCancel() {
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler3Waiting, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var session = new LSProcessSession(new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        result = session.Cancel();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(session.RootNode.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(root.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void Execute_Modification() {
        var selector = LSProcessNodeSelector.Create("sel", 0);
        var handler = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);

        // Add child before processing
        selector.AddChild(handler);
        Assert.That(selector.HasChild("handler1"), Is.True);

        // Start processing
        var session = new LSProcessSession( new MockProcess(), selector);
        var result = session.Execute();

        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        Assert.Throws<LSException>(() => selector.AddChild(handler2));
        Assert.Throws<LSException>(() => selector.RemoveChild("handler1"));
        Assert.That(selector.HasChild("handler1"), Is.True);
        Assert.That(selector.HasChild("handler2"), Is.False);
    }

    [Test]
    public void Execution_Condition() {
        List<string> executionPath = new List<string>();
        var root = LSProcessNodeSelector.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("alwaysFailCondition", (session) => {
            executionPath.Add("SHOULD_NOT_EXECUTE");
            return LSProcessResultStatus.SUCCESS;
        }, 1, LSProcessPriority.HIGH, (proc, node) => false); // condition always false
        var handler2 = LSProcessNodeHandler.Create("conditionalSuccess", (session) => {
            executionPath.Add("CONDITIONAL_SUCCESS");
            return LSProcessResultStatus.SUCCESS;
        }, 2, LSProcessPriority.NORMAL, (proc, node) => true); // condition always true
        var handler3 = LSProcessNodeHandler.Create("fallback", (session) => {
            executionPath.Add("FALLBACK");
            return LSProcessResultStatus.SUCCESS;
        }, 3, LSProcessPriority.LOW); // no condition, always true
        root.AddChild(handler1);
        root.AddChild(handler2);
        root.AddChild(handler3);

        var result = new LSProcessSession(new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        // Only the conditional success should execute (first with true condition)
        Assert.That(executionPath, Is.EqualTo(new List<string> { "CONDITIONAL_SUCCESS" }));
    }
}
