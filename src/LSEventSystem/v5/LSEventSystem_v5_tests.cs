using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem.TestV5;

/// <summary>
/// Comprehensive NUnit tests for LSEventSystem v5, focusing on LSEventContextBuilder.
/// </summary>
[TestFixture]
public class LSEventSystemTestsV5 {
    public class MockEvent : LSEvent {
        public MockEvent(LSDispatcher? dispatcher) : base(dispatcher) { }
    }

    private LSDispatcher _mockDispatcher;
    private LSEventHandler _mockHandler1;
    private LSEventHandler _mockHandler2;
    private LSEventHandler _mockHandler3Failure;
    private LSEventHandler _mockHandler3Cancel;
    private LSEventHandler _mockHandler3Waiting;
    private int _handler1CallCount;
    private int _handler2CallCount;
    private int _handler3CallCount;

    [SetUp]
    public void Setup() {
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;

        _mockHandler1 = (evt, node) => {
            _handler1CallCount++;
            return LSEventProcessStatus.SUCCESS;
        };

        _mockHandler2 = (evt, node) => {
            _handler2CallCount++;
            return LSEventProcessStatus.SUCCESS;
        };

        _mockHandler3Failure = (evt, node) => {
            _handler3CallCount++;
            return LSEventProcessStatus.FAILURE;
        };

        _mockHandler3Cancel = (evt, node) => {
            _handler3CallCount++;
            return LSEventProcessStatus.CANCELLED;
        };

        _mockHandler3Waiting = (evt, node) => {
            _handler3CallCount++;
            return LSEventProcessStatus.WAITING;
        };

        _mockDispatcher = new LSDispatcher();
    }

    [TearDown]
    public void Cleanup() {
        // Reset for next test
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;
        _mockDispatcher = null!;
    }

    #region Basic Event Tests
    [Test]
    public void TestEventCreation() {
        var mockEvent = new MockEvent(_mockDispatcher);
        Assert.That(mockEvent, Is.Not.Null);
        Assert.That(mockEvent.ID, Is.Not.EqualTo(System.Guid.Empty));
        Assert.That(mockEvent.Dispatcher, Is.EqualTo(_mockDispatcher));
    }
    #endregion

    #region Basic Builder Tests

    [Test]
    public void TestBuilderBasicSequenceEmpty() {
        var context = new LSEventContextBuilder()
            .Sequence("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
    }
    [Test]
    public void TestBuilderBasicSequenceSuccess() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSequenceFailure() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Failure))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSequenceCancel() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Cancel))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.CANCELLED));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingResume() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingFailure() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingCancel() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        processContext.Cancel();
        Assert.That(processContext.IsCancelled, Is.True);
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelector() {
        var context = new LSEventContextBuilder()
            .Selector("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteSuccess() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteFailure() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler3Failure))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingResume() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingFail() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingCancel() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        processContext.Cancel();
        Assert.That(processContext.IsCancelled, Is.True);
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallel() {
        var context = new LSEventContextBuilder()
            .Parallel("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
    }
    [Test]
    public void TestBuilderParallelSuccess() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1), 1) // require 1 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelSuccess2() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1)
                .Execute("handler2", _mockHandler2), 2) // require 2 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelSuccess3() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1)
                .Execute("handler2", _mockHandler3Failure), 1) // require 1 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelFailure() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Failure), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelFailure2() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1)
                .Execute("handler2", _mockHandler3Failure), 2) // require 2 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithResume() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithFail() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1)
                .Execute("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        System.Console.WriteLine("[TestBuilderParallelWaitingSuccessWithFail] Processing event...");
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // handler1 should succeed immediately
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        System.Console.WriteLine("[TestBuilderParallelWaitingSuccessWithFail] Failing handler1...");
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // handler1 has already succeeded, this should not change anything
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithFailAndResume() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1)
                .Execute("handler2", _mockHandler3Waiting)
                .Execute("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.WAITING)); // we have 1 success, 1 failure and 1 waiting
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Resume("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // now we have 2 successes
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingFailureWithFail() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSEventProcessStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFailureWithResume() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Failure)
                .Execute("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Resume("handler2");
        Assert.That(resumeResult1, Is.EqualTo(LSEventProcessStatus.FAILURE)); // handler2 has failed but
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingSuccessResume() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting)
                .Execute("handler2", _mockHandler3Waiting)
                .Execute("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        Assert.That(context.HasChild("handler3"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Process result: " + result);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(3));
        // Simulate external resume
        var resumeResult1 = processContext.Resume("handler1");
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Resume handler1 result: " + resumeResult1);
        Assert.That(resumeResult1, Is.EqualTo(LSEventProcessStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
        var resumeResult2 = processContext.Resume("handler2");
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Resume handler2 result: " + resumeResult2);
        Assert.That(resumeResult2, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // now two have resumed, which meets the requirement of 2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
                                                        // handler3 is still waiting, but the parallel node has already succeeded
        var resumeResult3 = processContext.Resume("handler3");
        Assert.That(resumeResult3, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // handler3 should return SUCCESS as the parallel node has already succeeded
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting)
                .Execute("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSEventProcessStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSEventProcessStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail2() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler3Waiting)
                .Execute("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSEventProcessStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSEventProcessStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void TestBuilderNestedStructure() {
        var context = new LSEventContextBuilder()
            .Sequence("root", rootBuilder => rootBuilder
                .Sequence("childSequence", seqBuilder => seqBuilder
                    .Execute("handler1", _mockHandler1))
                .Selector("childSelector", selBuilder => selBuilder
                    .Execute("handler2", _mockHandler2))
                .Parallel("childParallel", parBuilder => parBuilder
                    .Execute("handler1B", _mockHandler1)
                    .Execute("handler2B", _mockHandler2), 2)
                )
            .Build();

        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("childSequence"), Is.True);
        Assert.That(context.HasChild("childSelector"), Is.True);
        Assert.That(context.HasChild("childParallel"), Is.True);

        var childSequence = context.GetChild("childSequence") as ILSEventLayerNode;
        Assert.That(childSequence, Is.Not.Null);
        Assert.That(childSequence.HasChild("handler1"), Is.True);

        var childSelector = context.GetChild("childSelector") as ILSEventLayerNode;
        Assert.That(childSelector, Is.Not.Null);
        Assert.That(childSelector.HasChild("handler2"), Is.True);

        var childParallel = context.GetChild("childParallel") as ILSEventLayerNode;
        Assert.That(childParallel, Is.Not.Null);
        Assert.That(childParallel.HasChild("handler1B"), Is.True);
        Assert.That(childParallel.HasChild("handler2B"), Is.True);

        // Test execution of the entire structure
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(2)); // handler1 and handler1B
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 and handler2B
    }

    [Test]
    public void TestBuilderWithContext() {
        var baseContext = new LSEventContextBuilder()
            .Sequence("base", baseBuilder => baseBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        var newContext = new LSEventContextBuilder(baseContext)
            .Sequence("newSequence", seqBuilder => seqBuilder
                .Execute("handler2", _mockHandler2))
            .Build();

        Assert.That(newContext.NodeID, Is.EqualTo("base"));
        Assert.That(newContext.HasChild("handler1"), Is.True);
        Assert.That(newContext.HasChild("newSequence"), Is.True);

        var newSequence = newContext.GetChild("newSequence") as ILSEventLayerNode;
        Assert.That(newSequence, Is.Not.Null);
        Assert.That(newSequence.HasChild("handler2"), Is.True);

        // Test execution of the entire structure
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, newContext);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler1
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // handler2
    }

    #endregion

    #region Basic Merge Tests

    [Test]
    public void TestBuilderMergeIntoContext() {
        var rootContext = new LSEventContextBuilder()
            .Sequence("root", seqBuilder => seqBuilder)
            .Build();

        Console.WriteLine($"Root context before merge: {rootContext.NodeID}");
        Console.WriteLine($"Root children before: {string.Join(", ", Array.ConvertAll(rootContext.GetChildren(), c => c.NodeID))}");

        var subContext = new LSEventContextBuilder()
            .Sequence("subContext", seqBuilder => seqBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        Console.WriteLine($"Sub context: {subContext.NodeID}");
        Console.WriteLine($"Sub context children: {string.Join(", ", Array.ConvertAll(subContext.GetChildren(), c => c.NodeID))}");

        var mergedContext = new LSEventContextBuilder(rootContext)
            .Merge(subContext)
            .Build();
        Console.WriteLine($"Merged context: {mergedContext.NodeID}");
        Console.WriteLine($"Merged children: {string.Join(", ", Array.ConvertAll(mergedContext.GetChildren(), c => c.NodeID))}");

        // The merged context should be the root context itself, containing the subContext as a child
        Assert.That(mergedContext.NodeID, Is.EqualTo("root")); // The merged context is the root
        Assert.That(mergedContext.HasChild("subContext"), Is.True); // The root should contain the subContext
        var subContextNode = mergedContext.GetChild("subContext") as ILSEventLayerNode;
        Assert.That(subContextNode, Is.Not.Null);
        Assert.That(subContextNode.HasChild("handler1"), Is.True);

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, mergedContext);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler1 should be called once

    }

    [Test]
    public void TestMergeNonConflictingNodes() {
        var contextA = new LSEventContextBuilder()
            .Sequence("sequenceA", seqBuilder => seqBuilder
                .Execute("handlerA", _mockHandler1))
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("sequenceB", seqBuilder => seqBuilder
                .Execute("handlerB", _mockHandler2))
            .Build();

        var root = new LSEventContextBuilder()
            .Sequence("root", rootBuilder => rootBuilder
                .Merge(contextA, mABuilder => mABuilder)
                .Merge(contextB, mBBuilder => mBBuilder))
            .Build();


        Assert.That(root, Is.Not.Null);
        Assert.That(root.HasChild("sequenceA"), Is.True);
        Assert.That(root.HasChild("sequenceB"), Is.True);

        var seqA = root.GetChild("sequenceA") as ILSEventLayerNode;
        var seqB = root.GetChild("sequenceB") as ILSEventLayerNode;
        Assert.That(seqA?.HasChild("handlerA"), Is.True);
        Assert.That(seqB?.HasChild("handlerB"), Is.True);

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, root);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handlerA should be called once
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // handlerB should be called once

    }

    [Test]
    public void TestMergeHandlerReplacement() {
        var contextA = new LSEventContextBuilder()
            .Sequence("sequence", seq => seq
                .Execute("handler", _mockHandler1))
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("sequence", seq => seq
                .Execute("handler", _mockHandler2))
            .Build();

        var sequence = new LSEventContextBuilder()
            .Merge(contextA, mA => mA)
            .Merge(contextB, mB => mB)
            .Build();

        Assert.That(sequence, Is.Not.Null);

        Assert.That(sequence.HasChild("handler"), Is.True);
        Assert.That(sequence.GetChildren().Length, Is.EqualTo(1));

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, sequence);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }

    #endregion

    #region Recursive Merge Tests

    [Test]
    public void TestDeepRecursiveMerge() {
        var contextA = new LSEventContextBuilder()
            .Sequence("level1", level1 => level1
                .Sequence("level2", level2 => level2
                    .Sequence("level3", level3 => level3
                        .Execute("handlerA", _mockHandler1)
                    )
                )
            )
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("level1", level1 => level1
                .Sequence("level2", level2 => level2
                    .Sequence("level3", level3 => level3
                        .Execute("handlerB", _mockHandler2)
                    )
                )
            )
            .Build();

        var root = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        // Navigate to the deep level and verify both handlers exist
        var level1 = root.GetChild("level1") as ILSEventLayerNode;
        Assert.That(level1, Is.Not.Null);
        var level2 = level1?.GetChild("level2") as ILSEventLayerNode;
        Assert.That(level2, Is.Not.Null);
        var level3 = level2?.GetChild("level3") as ILSEventLayerNode;
        Assert.That(level3, Is.Not.Null);

        Assert.That(level3.HasChild("handlerA"), Is.True);
        Assert.That(level3.HasChild("handlerB"), Is.True);
        // we don't need to test execution here, just structure
    }

    [Test]
    public void TestMixedNodeTypeMerge() {
        var contextA = new LSEventContextBuilder()
            .Sequence("container", container => container
                .Execute("handler1", _mockHandler1)
            )
            .Build();

        var contextB = new LSEventContextBuilder()
            .Selector("container", selector => selector
                .Execute("handler2", _mockHandler2)
            )
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        // The container should be replaced with the selector from contextB
        Assert.That(mergedContext.HasChild("container"), Is.True);
        var container = mergedContext.GetChild("container");
        Assert.That(container, Is.InstanceOf<LSEventSelectorNode>());

        var containerLayer = container as ILSEventLayerNode;
        Assert.That(containerLayer?.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestComplexHierarchyMerge() {
        // Create a complex source context
        var sourceContext = new LSEventContextBuilder()
            .Sequence("mainSequence", mainSeq => mainSeq
                .Selector("validation", validation => validation
                    .Execute("validateUser", _mockHandler1)
                    .Execute("validatePermissions", _mockHandler2)
                )
                .Sequence("execution", execution => execution
                    .Execute("executeAction", _mockHandler1)
                )
            )
            .Build();

        // Create a target context with partial overlap
        var targetContext = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Sequence("mainSequence", mainSeq => mainSeq
                    .Sequence("execution", execution => execution
                        .Execute("preExecute", _mockHandler1)
                    )
                    .Execute("cleanup", _mockHandler2)
                )
            )
            .Build();

        var mergedContext = new LSEventContextBuilder(targetContext)
            .Merge(sourceContext)
            .Build();

        // Verify the structure
        var mainSeq = mergedContext.GetChild("mainSequence") as ILSEventLayerNode;
        Assert.That(mainSeq, Is.Not.Null);

        // Should have validation (new), execution (merged), and cleanup (existing)
        Assert.That(mainSeq.HasChild("validation"), Is.True);
        Assert.That(mainSeq.HasChild("execution"), Is.True);
        Assert.That(mainSeq.HasChild("cleanup"), Is.True);

        // Check validation node
        var validation = mainSeq.GetChild("validation") as ILSEventLayerNode;
        Assert.That(validation, Is.InstanceOf<LSEventSelectorNode>());
        Assert.That(validation?.HasChild("validateUser"), Is.True);
        Assert.That(validation?.HasChild("validatePermissions"), Is.True);

        // Check execution node - should have both handlers
        var execution = mainSeq.GetChild("execution") as ILSEventLayerNode;
        Assert.That(execution?.HasChild("preExecute"), Is.True);
        Assert.That(execution?.HasChild("executeAction"), Is.True);
    }

    #endregion

    #region Error Condition Tests

    [Test]
    public void TestMergeNullContext() {
        var builder = new LSEventContextBuilder()
            .Sequence("root");

        Assert.Throws<LSArgumentNullException>(() => builder.Merge(null!));
    }

    //TODO: this test is a little wierd, maybe we need to check if this is the intended behavior
    [Test]
    public void TestMergeWithoutCurrentNode() {
        var sourceContext = new LSEventContextBuilder()
            .Sequence("sequence", seq => seq
                .Execute("handler", _mockHandler1))
            .Build();

        var builder = new LSEventContextBuilder();

        var mergedContext = builder.Merge(sourceContext).Build();

        Assert.That(mergedContext, Is.Not.Null);
    }

    [Test]
    public void TestRemoveNonExistentNode() {
        var builder = new LSEventContextBuilder()
            .Sequence("root");

        // Should not throw, just return false or handle gracefully
        Assert.DoesNotThrow(() => builder.RemoveChild("nonExistent"));
    }

    #endregion

    #region Additional Functionality Tests

    [Test]
    public void TestMergeEmptyContext() {
        var emptyContext = new LSEventContextBuilder()
            .Sequence("empty")
            .Build();

        var populatedContext = new LSEventContextBuilder()
            .Sequence("populated", seq => seq
                .Execute("handler1", _mockHandler1))
            .Build();

        var mergedContext = new LSEventContextBuilder(populatedContext)
            .Merge(emptyContext)
            .Build();

        Assert.That(mergedContext.HasChild("handler1"), Is.True);
        Assert.That(mergedContext.HasChild("empty"), Is.True);
        var emptyNode = mergedContext.GetChild("empty") as ILSEventLayerNode;
        Assert.That(emptyNode?.GetChildren().Length, Is.EqualTo(0));
    }

    [Test]
    public void TestMergeWithPriority() {
        var contextA = new LSEventContextBuilder()
            .Sequence("sequence", seq => seq
                .Execute("handler", _mockHandler1, LSPriority.NORMAL))
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("sequence", seq => seq
                .Execute("handler", _mockHandler2, LSPriority.HIGH))
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        Assert.That(mergedContext.HasChild("handler"), Is.True);

        // Since override is always allowed, the second handler should replace the first
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, mergedContext);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }

    [Test]
    public void TestRemoveChildScenarios() {
        var context = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Execute("handler1", _mockHandler1)
                .Sequence("childSeq", child => child
                    .Execute("handler2", _mockHandler2)))
            .Build();

        var builder = new LSEventContextBuilder(context);
        builder.RemoveChild("handler1");
        builder.RemoveChild("childSeq");

        var updatedContext = builder.Build();
        Assert.That(updatedContext.HasChild("handler1"), Is.False);
        Assert.That(updatedContext.HasChild("childSeq"), Is.False);
        Assert.That(updatedContext.GetChildren().Length, Is.EqualTo(0));
    }

    [Test]
    public void TestHandlerWithConditionsFalse() {
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("conditionalHandler", (ctx, node) => {
                    _handler1CallCount++;
                    return LSEventProcessStatus.FAILURE;
                }, LSPriority.NORMAL, (evt, node) => false)) // skip the handler
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);

        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // since we skipped the handler that would return FAILURE the result is SUCCESS
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // handler should not be called because conditionMet == false

    }
    [Test]
    public void TestHandlerWithConditionsTrue() {
        var context = new LSEventContextBuilder()
                    .Sequence("root", seq => seq
                        .Execute("conditionalHandler", (ctx, node) => {
                            _handler1CallCount++;
                            return LSEventProcessStatus.FAILURE;
                        }, LSPriority.NORMAL, (evt, node) => true)) // condition met
                    .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, context);

        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // since the handler returns FAILURE the result is FAILURE
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler should be called because handler condition is true

    }

    [Test]
    public void TestBuildEmptyBuilder() {
        var builder = new LSEventContextBuilder();
        Assert.Throws<LSException>(() => builder.Build());
    }

    [Test]
    public void TestMultipleBuilds() {
        var builder = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("handler1", _mockHandler1));

        var context1 = builder.Build();
        Assert.That(context1.HasChild("handler1"), Is.True);

        // Modify builder after first build
        builder.Execute("handler2", _mockHandler2); //this could even throw an exception, but for now we allow it

        Assert.Throws<LSException>(() => builder.Build()); // Building again without changes should throw
    }

    [Test]
    public void TestExecutionPreserveOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        var contextA = new LSEventContextBuilder()
            .Sequence("seqA", seq => seq
                .Execute("handlerA", (evt, node) => {
                    executionOrder.Add("A");
                    return LSEventProcessStatus.SUCCESS;
                }))
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("seqB", seq => seq
                .Execute("handlerB", (evt, node) => {
                    executionOrder.Add("B");
                    return LSEventProcessStatus.SUCCESS;
                }))
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB))
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, mergedContext);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));

        // in the same priority order should be preserved
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "A", "B" }));

    }

    public void TestExecutionPriorityOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        // Create fresh contexts for the second test
        var contextA = new LSEventContextBuilder()
            .Sequence("seqA", seq => seq
                .Execute("handlerA", (evt, node) => {
                    executionOrder.Add("A");
                    return LSEventProcessStatus.SUCCESS;
                }))
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("seqB", seq => seq
                .Execute("handlerB", (evt, node) => {
                    executionOrder.Add("B");
                    return LSEventProcessStatus.SUCCESS;
                }))
            .Build();

        var contextC = new LSEventContextBuilder()
            .Sequence("seqC", seq => seq
                .Execute("handlerC", (evt, node) => {
                    executionOrder.Add("C");
                    return LSEventProcessStatus.SUCCESS;
                }), LSPriority.HIGH)
            .Build();
        var mergedContextWithPriority = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextC) // C has higher priority
                .Merge(contextB))
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent, mergedContextWithPriority);
        var result = processContext.Process();
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));

        Assert.That(executionOrder, Is.EqualTo(new List<string> { "C", "A", "B" }));
    }

    #endregion
}




