namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;
/// <summary>
/// Tests for LSProcessNodeSequence functionality.
/// </summary>
[TestFixture]
public class NodeSequence_Tests {
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
        LSProcessManager.DebugLogging();
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
        var root = LSProcessNodeSequence.Create("root", 0);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        // Test execution
        var process = new MockProcess();
        var session = new LSProcessSession(null!, process, root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void Execute_Success() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler1, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution
        var result = new LSProcessSession(null!, new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
    }

    [Test]
    public void Execute_Failure() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Failure, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 2);
        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var result = new LSProcessSession(null!, new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Should not be called
    }

    [Test]
    public void Execute_Sequence_Cancel() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Cancel, 1);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 2);

        root.AddChild(handler1);
        root.AddChild(handler2);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        Assert.That(root.HasChild("handler2"), Is.True);
        // Test execution
        var result = new LSProcessSession(null!, new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Should not be called
    }

    [Test]
    public void Waiting_Resume() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        root.AddChild(handler1);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var session = new LSProcessSession(null!, new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = session.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void Execute_Waiting_Failure() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        root.AddChild(handler1);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var session = new LSProcessSession(null!, new MockProcess(), root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = session.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void Execute_Waiting_Cancel() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler3Waiting, 1);
        root.AddChild(handler1);

        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);
        // Test execution
        var session = new LSProcessSession(null!, new MockProcess(), root);
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
    public void Execute_WithModification() {
        var sequence = LSProcessNodeSequence.Create("seq", 0);
        var handler = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);

        // Should be able to add child before processing
        Assert.DoesNotThrow(() => sequence.AddChild(handler));
        Assert.That(sequence.HasChild("handler1"), Is.True);

        var session = new LSProcessSession(null!, new MockProcess(), sequence);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // Should not be able to add/remove children after processing started
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        Assert.Throws<LSException>(() => sequence.AddChild(handler2));
        Assert.Throws<LSException>(() => sequence.RemoveChild("handler1"));
    }

    [Test]
    public void Execute_WithConditionsFalse() {
        var root = LSProcessNodeSequence.Create("root", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", (session) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        }, 0, conditions: (proc) => false);
        root.AddChild(handler1);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("handler1"), Is.True);

        var result = new LSProcessSession(null!, new MockProcess(), root).Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // Handler should not be called
    }
}
