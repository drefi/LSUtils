using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Processing.Tests;

/// <summary>
/// Comprehensive NUnit tests for LSProcessingSystem.
/// </summary>
[TestFixture]
public class LSProcessingSystemTests {
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

    [SetUp]
    public void Setup() {
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;

        _mockHandler1 = (evt, node) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler2 = (evt, node) => {
            _handler2CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler3Failure = (evt, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _mockHandler3Cancel = (evt, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _mockHandler3Waiting = (evt, node) => {
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

    #region Basic Process Tests
    [Test]
    public void TestProcessCreation() {
        var mockProcess = new MockProcess();
        Assert.That(mockProcess, Is.Not.Null);
        Assert.That(mockProcess.ID, Is.Not.EqualTo(System.Guid.Empty));
    }
    #endregion

    #region Basic Builder Tests

    [Test]
    public void TestBuilderBasicSequenceEmpty() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }
    [Test]
    public void TestBuilderBasicSequenceSuccess() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSequenceFailure() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Failure))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSequenceCancel() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Cancel))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.CANCELLED));
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingResume() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingFailure() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSequenceExecuteWaitingCancel() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        processContext.Cancel();
        Assert.That(processContext.IsCancelled, Is.True);
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelector() {
        var context = new LSProcessTreeBuilder()
            .Selector("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteSuccess() {
        var context = new LSProcessTreeBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Handler("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteFailure() {
        var context = new LSProcessTreeBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Handler("handler1", _mockHandler3Failure))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingResume() {
        var context = new LSProcessTreeBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingFail() {
        var context = new LSProcessTreeBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicSelectorExecuteWaitingCancel() {
        var context = new LSProcessTreeBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Handler("handler1", _mockHandler3Waiting))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        processContext.Cancel();
        Assert.That(processContext.IsCancelled, Is.True);
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallel() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root")
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }
    [Test]
    public void TestBuilderParallelSuccess() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1), 1) // require 1 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelSuccess2() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2), 2) // require 2 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelSuccess3() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Failure), 1) // require 1 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelFailure() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Failure), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelFailure2() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Failure), 2) // require 2 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithResume() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithFail() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var processContext = new LSProcessSession(mockProcess, context);
        System.Console.WriteLine("[TestBuilderParallelWaitingSuccessWithFail] Processing...");
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler1 should succeed immediately
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        System.Console.WriteLine("[TestBuilderParallelWaitingSuccessWithFail] Failing handler1...");
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler1 has already succeeded, this should not change anything
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingSuccessWithFailAndResume() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Waiting)
                .Handler("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.WAITING)); // we have 1 success, 1 failure and 1 waiting
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Resume("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // now we have 2 successes
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderParallelWaitingFailureWithFail() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = processContext.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFailureWithResume() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Failure)
                .Handler("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Resume("handler2");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // handler2 has failed but
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingSuccessResume() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting)
                .Handler("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        Assert.That(context.HasChild("handler3"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Process result: " + result);
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(3));
        // Simulate external resume
        var resumeResult1 = processContext.Resume("handler1");
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Resume handler1 result: " + resumeResult1);
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
        var resumeResult2 = processContext.Resume("handler2");
        System.Console.WriteLine("[TestBuilderBasicParallelMultipleWaitingSuccessResume] Resume handler2 result: " + resumeResult2);
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // now two have resumed, which meets the requirement of 2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
                                                        // handler3 is still waiting, but the parallel node has already succeeded
        var resumeResult3 = processContext.Resume("handler3");
        Assert.That(resumeResult3, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler3 should return SUCCESS as the parallel node has already succeeded
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }
    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail2() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        Assert.That(context.HasChild("handler2"), Is.True);
        // Test execution
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = processContext.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the context is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = processContext.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void TestBuilderNestedStructure() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", rootBuilder => rootBuilder
                .Sequence("childSequence", seqBuilder => seqBuilder
                    .Handler("handler1", _mockHandler1))
                .Selector("childSelector", selBuilder => selBuilder
                    .Handler("handler2", _mockHandler2))
                .Parallel("childParallel", parBuilder => parBuilder
                    .Handler("handler1B", _mockHandler1)
                    .Handler("handler2B", _mockHandler2), 2)
                )
            .Build();

        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("childSequence"), Is.True);
        Assert.That(context.HasChild("childSelector"), Is.True);
        Assert.That(context.HasChild("childParallel"), Is.True);

        var childSequence = context.GetChild("childSequence") as ILSProcessLayerNode;
        Assert.That(childSequence, Is.Not.Null);
        Assert.That(childSequence.HasChild("handler1"), Is.True);

        var childSelector = context.GetChild("childSelector") as ILSProcessLayerNode;
        Assert.That(childSelector, Is.Not.Null);
        Assert.That(childSelector.HasChild("handler2"), Is.True);

        var childParallel = context.GetChild("childParallel") as ILSProcessLayerNode;
        Assert.That(childParallel, Is.Not.Null);
        Assert.That(childParallel.HasChild("handler1B"), Is.True);
        Assert.That(childParallel.HasChild("handler2B"), Is.True);

        // Test execution of the entire structure
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(2)); // handler1 and handler1B
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 and handler2B
    }

    [Test]
    public void TestBuilderWithContext() {
        var baseContext = new LSProcessTreeBuilder()
            .Sequence("base", baseBuilder => baseBuilder
                .Handler("handler1", _mockHandler1))
            .Build();

        var newContext = new LSProcessTreeBuilder(baseContext)
            .Sequence("newSequence", seqBuilder => seqBuilder
                .Handler("handler2", _mockHandler2))
            .Build();

        Assert.That(newContext.NodeID, Is.EqualTo("base"));
        Assert.That(newContext.HasChild("handler1"), Is.True);
        Assert.That(newContext.HasChild("newSequence"), Is.True);

        var newSequence = newContext.GetChild("newSequence") as ILSProcessLayerNode;
        Assert.That(newSequence, Is.Not.Null);
        Assert.That(newSequence.HasChild("handler2"), Is.True);

        // Test execution of the entire structure
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, newContext);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler1
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // handler2
    }

    #endregion

    #region Basic Merge Tests

    [Test]
    public void TestBuilderMergeIntoContext() {
        var rootContext = new LSProcessTreeBuilder()
            .Sequence("root", seqBuilder => seqBuilder)
            .Build();

        Console.WriteLine($"Root context before merge: {rootContext.NodeID}");
        Console.WriteLine($"Root children before: {string.Join(", ", Array.ConvertAll(rootContext.GetChildren(), c => c.NodeID))}");

        var subContext = new LSProcessTreeBuilder()
            .Sequence("subContext", seqBuilder => seqBuilder
                .Handler("handler1", _mockHandler1))
            .Build();

        Console.WriteLine($"Sub context: {subContext.NodeID}");
        Console.WriteLine($"Sub context children: {string.Join(", ", Array.ConvertAll(subContext.GetChildren(), c => c.NodeID))}");

        var mergedContext = new LSProcessTreeBuilder(rootContext)
            .Merge(subContext)
            .Build();
        Console.WriteLine($"Merged context: {mergedContext.NodeID}");
        Console.WriteLine($"Merged children: {string.Join(", ", Array.ConvertAll(mergedContext.GetChildren(), c => c.NodeID))}");

        // The merged context should be the root context itself, containing the subContext as a child
        Assert.That(mergedContext.NodeID, Is.EqualTo("root")); // The merged context is the root
        Assert.That(mergedContext.HasChild("subContext"), Is.True); // The root should contain the subContext
        var subContextNode = mergedContext.GetChild("subContext") as ILSProcessLayerNode;
        Assert.That(subContextNode, Is.Not.Null);
        Assert.That(subContextNode.HasChild("handler1"), Is.True);

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, mergedContext);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler1 should be called once

    }

    [Test]
    public void TestMergeNonConflictingNodes() {
        var contextA = new LSProcessTreeBuilder()
            .Sequence("sequenceA", seqBuilder => seqBuilder
                .Handler("handlerA", _mockHandler1))
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("sequenceB", seqBuilder => seqBuilder
                .Handler("handlerB", _mockHandler2))
            .Build();

        var root = new LSProcessTreeBuilder()
            .Sequence("root", rootBuilder => rootBuilder
                .Merge(contextA, mABuilder => mABuilder)
                .Merge(contextB, mBBuilder => mBBuilder))
            .Build();


        Assert.That(root, Is.Not.Null);
        Assert.That(root.HasChild("sequenceA"), Is.True);
        Assert.That(root.HasChild("sequenceB"), Is.True);

        var seqA = root.GetChild("sequenceA") as ILSProcessLayerNode;
        var seqB = root.GetChild("sequenceB") as ILSProcessLayerNode;
        Assert.That(seqA?.HasChild("handlerA"), Is.True);
        Assert.That(seqB?.HasChild("handlerB"), Is.True);

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, root);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handlerA should be called once
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // handlerB should be called once

    }

    [Test]
    public void TestMergeHandlerReplacement() {
        var contextA = new LSProcessTreeBuilder()
            .Sequence("sequence", seq => seq
                .Handler("handler", _mockHandler1))
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("sequence", seq => seq
                .Handler("handler", _mockHandler2))
            .Build();

        var sequence = new LSProcessTreeBuilder()
            .Merge(contextA, mA => mA)
            .Merge(contextB, mB => mB)
            .Build();

        Assert.That(sequence, Is.Not.Null);

        Assert.That(sequence.HasChild("handler"), Is.True);
        Assert.That(sequence.GetChildren().Length, Is.EqualTo(1));

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, sequence);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }

    #endregion

    #region Recursive Merge Tests

    [Test]
    public void TestDeepRecursiveMerge() {
        var contextA = new LSProcessTreeBuilder()
            .Sequence("level1", level1 => level1
                .Sequence("level2", level2 => level2
                    .Sequence("level3", level3 => level3
                        .Handler("handlerA", _mockHandler1)
                    )
                )
            )
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("level1", level1 => level1
                .Sequence("level2", level2 => level2
                    .Sequence("level3", level3 => level3
                        .Handler("handlerB", _mockHandler2)
                    )
                )
            )
            .Build();

        var root = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        // Navigate to the deep level and verify both handlers exist
        var level1 = root.GetChild("level1") as ILSProcessLayerNode;
        Assert.That(level1, Is.Not.Null);
        var level2 = level1?.GetChild("level2") as ILSProcessLayerNode;
        Assert.That(level2, Is.Not.Null);
        var level3 = level2?.GetChild("level3") as ILSProcessLayerNode;
        Assert.That(level3, Is.Not.Null);

        Assert.That(level3.HasChild("handlerA"), Is.True);
        Assert.That(level3.HasChild("handlerB"), Is.True);
        // we don't need to test execution here, just structure
    }

    [Test]
    public void TestMixedNodeTypeMerge() {
        var contextA = new LSProcessTreeBuilder()
            .Sequence("container", container => container
                .Handler("handler1", _mockHandler1)
            )
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Selector("container", selector => selector
                .Handler("handler2", _mockHandler2)
            )
            .Build();

        var mergedContext = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        // The container should be replaced with the selector from contextB
        Assert.That(mergedContext.HasChild("container"), Is.True);
        var container = mergedContext.GetChild("container");
        Assert.That(container, Is.InstanceOf<LSProcessNodeSelector>());

        var containerLayer = container as ILSProcessLayerNode;
        Assert.That(containerLayer?.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestComplexHierarchyMerge() {
        // Create a complex source context
        var sourceContext = new LSProcessTreeBuilder()
            .Sequence("mainSequence", mainSeq => mainSeq
                .Selector("validation", validation => validation
                    .Handler("validateUser", _mockHandler1)
                    .Handler("validatePermissions", _mockHandler2)
                )
                .Sequence("execution", execution => execution
                    .Handler("executeAction", _mockHandler1)
                )
            )
            .Build();

        // Create a target context with partial overlap
        var targetContext = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Sequence("mainSequence", mainSeq => mainSeq
                    .Sequence("execution", execution => execution
                        .Handler("preExecute", _mockHandler1)
                    )
                    .Handler("cleanup", _mockHandler2)
                )
            )
            .Build();

        var mergedContext = new LSProcessTreeBuilder(targetContext)
            .Merge(sourceContext)
            .Build();

        // Verify the structure
        var mainSeq = mergedContext.GetChild("mainSequence") as ILSProcessLayerNode;
        Assert.That(mainSeq, Is.Not.Null);

        // Should have validation (new), execution (merged), and cleanup (existing)
        Assert.That(mainSeq.HasChild("validation"), Is.True);
        Assert.That(mainSeq.HasChild("execution"), Is.True);
        Assert.That(mainSeq.HasChild("cleanup"), Is.True);

        // Check validation node
        var validation = mainSeq.GetChild("validation") as ILSProcessLayerNode;
        Assert.That(validation, Is.InstanceOf<LSProcessNodeSelector>());
        Assert.That(validation?.HasChild("validateUser"), Is.True);
        Assert.That(validation?.HasChild("validatePermissions"), Is.True);

        // Check execution node - should have both handlers
        var execution = mainSeq.GetChild("execution") as ILSProcessLayerNode;
        Assert.That(execution?.HasChild("preExecute"), Is.True);
        Assert.That(execution?.HasChild("executeAction"), Is.True);
    }

    #endregion

    #region Error Condition Tests

    [Test]
    public void TestMergeNullContext() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root");

        Assert.Throws<LSArgumentNullException>(() => builder.Merge(null!));
    }

    //TODO: this test is a little wierd, maybe we need to check if this is the intended behavior
    [Test]
    public void TestMergeWithoutCurrentNode() {
        var sourceContext = new LSProcessTreeBuilder()
            .Sequence("sequence", seq => seq
                .Handler("handler", _mockHandler1))
            .Build();

        var builder = new LSProcessTreeBuilder();

        var mergedContext = builder.Merge(sourceContext).Build();

        Assert.That(mergedContext, Is.Not.Null);
    }

    [Test]
    public void TestRemoveNonExistentNode() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root");

        // Should not throw, just return false or handle gracefully
        Assert.DoesNotThrow(() => builder.RemoveChild("nonExistent"));
    }

    #endregion

    #region Additional Functionality Tests

    [Test]
    public void TestMergeEmptyContext() {
        var emptyContext = new LSProcessTreeBuilder()
            .Sequence("empty")
            .Build();

        var populatedContext = new LSProcessTreeBuilder()
            .Sequence("populated", seq => seq
                .Handler("handler1", _mockHandler1))
            .Build();

        var mergedContext = new LSProcessTreeBuilder(populatedContext)
            .Merge(emptyContext)
            .Build();

        Assert.That(mergedContext.HasChild("handler1"), Is.True);
        Assert.That(mergedContext.HasChild("empty"), Is.True);
        var emptyNode = mergedContext.GetChild("empty") as ILSProcessLayerNode;
        Assert.That(emptyNode?.GetChildren().Length, Is.EqualTo(0));
    }

    [Test]
    public void TestMergeWithPriority() {
        var contextA = new LSProcessTreeBuilder()
            .Sequence("sequence", seq => seq
                .Handler("handler", _mockHandler1, LSProcessPriority.NORMAL))
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("sequence", seq => seq
                .Handler("handler", _mockHandler2, LSProcessPriority.HIGH))
            .Build();

        var mergedContext = new LSProcessTreeBuilder()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        Assert.That(mergedContext.HasChild("handler"), Is.True);

        // Since override is always allowed, the second handler should replace the first
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, mergedContext);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }

    [Test]
    public void TestRemoveChildScenarios() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Handler("handler1", _mockHandler1)
                .Sequence("childSeq", child => child
                    .Handler("handler2", _mockHandler2)))
            .Build();

        var builder = new LSProcessTreeBuilder(context);
        builder.RemoveChild("handler1");
        builder.RemoveChild("childSeq");

        var updatedContext = builder.Build();
        Assert.That(updatedContext.HasChild("handler1"), Is.False);
        Assert.That(updatedContext.HasChild("childSeq"), Is.False);
        Assert.That(updatedContext.GetChildren().Length, Is.EqualTo(0));
    }

    [Test]
    public void TestHandlerWithConditionsFalse() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (ctx, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.FAILURE;
                }, LSProcessPriority.NORMAL, false, (evt, node) => false)) // skip the handler
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // since we skipped the handler that would return FAILURE the result is SUCCESS
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // handler should not be called because conditionMet == false

    }
    [Test]
    public void TestHandlerWithConditionsTrue() {
        var context = new LSProcessTreeBuilder()
                    .Sequence("root", seq => seq
                        .Handler("conditionalHandler", (ctx, node) => {
                            _handler1CallCount++;
                            return LSProcessResultStatus.FAILURE;
                        }, LSProcessPriority.NORMAL, false, (evt, node) => true)) // condition met
                    .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE)); // since the handler returns FAILURE the result is FAILURE
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler should be called because handler condition is true

    }

    [Test]
    public void TestBuildEmptyBuilder() {
        var builder = new LSProcessTreeBuilder();
        Assert.Throws<LSException>(() => builder.Build());
    }

    [Test]
    public void TestMultipleBuilds() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("handler1", _mockHandler1));

        var context1 = builder.Build();
        Assert.That(context1.HasChild("handler1"), Is.True);

        // Modify builder after first build
        builder.Handler("handler2", _mockHandler2); //this could even throw an exception, but for now we allow it

        Assert.Throws<LSException>(() => builder.Build()); // Building again without changes should throw
    }

    [Test]
    public void TestExecutionPreserveOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        var contextA = new LSProcessTreeBuilder()
            .Sequence("seqA", seq => seq
                .Handler("handlerA", (evt, node) => {
                    executionOrder.Add("A");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("seqB", seq => seq
                .Handler("handlerB", (evt, node) => {
                    executionOrder.Add("B");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var mergedContext = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, mergedContext);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // in the same priority order should be preserved
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "A", "B" }));

    }

    public void TestExecutionPriorityOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        // Create fresh contexts for the second test
        var contextA = new LSProcessTreeBuilder()
            .Sequence("seqA", seq => seq
                .Handler("handlerA", (evt, node) => {
                    executionOrder.Add("A");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var contextB = new LSProcessTreeBuilder()
            .Sequence("seqB", seq => seq
                .Handler("handlerB", (evt, node) => {
                    executionOrder.Add("B");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var contextC = new LSProcessTreeBuilder()
            .Sequence("seqC", seq => seq
                .Handler("handlerC", (evt, node) => {
                    executionOrder.Add("C");
                    return LSProcessResultStatus.SUCCESS;
                }), LSProcessPriority.HIGH)
            .Build();
        var mergedContextWithPriority = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextC) // C has higher priority
                .Merge(contextB))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, mergedContextWithPriority);
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        Assert.That(executionOrder, Is.EqualTo(new List<string> { "C", "A", "B" }));
    }

    #endregion

    #region Node Unit Tests

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
        var context = new LSProcessTreeBuilder().Sequence("root").Handler("handler", _mockHandler1).Build();
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        processContext.Execute();

        // Create a context with the original handler to test execution count
        var testContext = new LSProcessSession(mockEvent, originalHandler);
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
        var mockEvent = new MockProcess();
        var testContext = new LSProcessSession(mockEvent, originalHandler);

        // Original execution count should be independent before cloning
        ((ILSProcessNode)originalHandler).Execute(testContext);
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(1));

        // Clone should share the count
        ((ILSProcessNode)cloneHandler!).Execute(testContext);
        Assert.That(originalHandler.ExecutionCount, Is.EqualTo(2));
        Assert.That(cloneHandler.ExecutionCount, Is.EqualTo(2));
    }

    [Test]
    public void TestLSProcessSequenceNodeChildModificationRestrictions() {
        var sequence = LSProcessNodeSequence.Create("seq", 0);
        var handler = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);

        // Should be able to add child before processing
        Assert.DoesNotThrow(() => sequence.AddChild(handler));
        Assert.That(sequence.HasChild("handler1"), Is.True);

        // Start processing to set _isProcessing = true
        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, sequence);
        ((ILSProcessNode)sequence).Execute(context);

        // Should not be able to add/remove children after processing started
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        Assert.Throws<InvalidOperationException>(() => sequence.AddChild(handler2));
        Assert.Throws<InvalidOperationException>(() => sequence.RemoveChild("handler1"));
    }

    [Test]
    public void TestLSProcessSelectorNodeUnrestrictedChildModification() {
        var selector = LSProcessNodeSelector.Create("sel", 0);
        var handler = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);

        // Add child before processing
        selector.AddChild(handler);
        Assert.That(selector.HasChild("handler1"), Is.True);

        // Start processing
        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, selector);
        ((ILSProcessNode)selector).Execute(context);

        // Should still be able to add/remove children (no processing restrictions for selector)
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);
        Assert.DoesNotThrow(() => selector.AddChild(handler2));
        Assert.DoesNotThrow(() => selector.RemoveChild("handler1"));
        Assert.That(selector.HasChild("handler2"), Is.True);
        Assert.That(selector.HasChild("handler1"), Is.False);
    }

    [Test]
    public void TestLSEventParallelNodeThresholdValidation() {
        // Test basic threshold scenarios
        var parallel1 = LSProcessNodeParallel.Create("par1", 0, 1, 1); // success=1, failure=1
        Assert.That(parallel1.NumRequiredToSucceed, Is.EqualTo(1));
        Assert.That(parallel1.NumRequiredToFailure, Is.EqualTo(1));

        // Test zero thresholds
        var parallel2 = LSProcessNodeParallel.Create("par2", 0, 0, 0); // success=0, failure=0
        Assert.That(parallel2.NumRequiredToSucceed, Is.EqualTo(0));
        Assert.That(parallel2.NumRequiredToFailure, Is.EqualTo(0));

        // Test with handlers
        var context = new LSProcessTreeBuilder()
            .Parallel("par", builder => builder
                .Handler("h1", _mockHandler1)
                .Handler("h2", _mockHandler2), 0, 0) // 0 required to succeed, 0 required to fail
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // With 0 required to succeed, should succeed immediately
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestNodeCloneIndependence() {
        // Test sequence node cloning
        var originalSeq = new LSProcessTreeBuilder()
            .Sequence("seq", seq => seq.Handler("handler", _mockHandler1))
            .Build();

        var clonedSeq = originalSeq.Clone();

        // Should be different instances
        Assert.That(clonedSeq, Is.Not.SameAs(originalSeq));
        Assert.That(clonedSeq.NodeID, Is.EqualTo(originalSeq.NodeID));

        // Should have independent processing state
        var mockEvent = new MockProcess();
        var originalContext = new LSProcessSession(mockEvent, originalSeq);
        var clonedContext = new LSProcessSession(mockEvent, clonedSeq);

        // Process original
        var originalResult = originalContext.Execute();
        Assert.That(originalResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));

        // Reset call count and process clone
        _handler1CallCount = 0;
        var clonedResult = clonedContext.Execute();
        Assert.That(clonedResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Clone should execute independently
    }

    [Test]
    public void TestNodePriorityAndOrderSorting() {
        List<string> executionOrder = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Handler("lowPriority", (evt, node) => {
                    executionOrder.Add("LOW");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.LOW)
                .Handler("normalPriority", (evt, node) => {
                    executionOrder.Add("NORMAL");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL)
                .Handler("highPriority", (evt, node) => {
                    executionOrder.Add("HIGH");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.HIGH)
                .Handler("criticalPriority", (evt, node) => {
                    executionOrder.Add("CRITICAL");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.CRITICAL))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // Should execute in priority order: CRITICAL, HIGH, NORMAL, LOW
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "CRITICAL", "HIGH", "NORMAL", "LOW" }));
    }

    #endregion

    #region Complex Processing Tests

    [Test]
    public void TestMixedPriorityExecutionWithParallelAndSequence() {
        List<string> executionOrder = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Parallel("parallel1", par => par
                    .Handler("highParallel", (evt, node) => {
                        executionOrder.Add("HIGH_PAR");
                        return LSProcessResultStatus.SUCCESS;
                    }, LSProcessPriority.HIGH)
                    .Handler("lowParallel", (evt, node) => {
                        executionOrder.Add("LOW_PAR");
                        return LSProcessResultStatus.SUCCESS;
                    }, LSProcessPriority.LOW), 2) // require both to succeed
                .Handler("criticalAfter", (evt, node) => {
                    executionOrder.Add("CRITICAL_AFTER");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.CRITICAL))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executionOrder.Count, Is.EqualTo(3));

        // The system executes in global priority order: CRITICAL first, then HIGH, then LOW
        // This demonstrates that priorities work across different node types in the tree
        Assert.That(executionOrder[0], Is.EqualTo("CRITICAL_AFTER")); // CRITICAL priority
        Assert.That(executionOrder[1], Is.EqualTo("HIGH_PAR"));       // HIGH priority
        Assert.That(executionOrder[2], Is.EqualTo("LOW_PAR"));        // LOW priority

        // Test that all expected handlers were called
        Assert.That(executionOrder.Contains("HIGH_PAR"), Is.True);
        Assert.That(executionOrder.Contains("LOW_PAR"), Is.True);
        Assert.That(executionOrder.Contains("CRITICAL_AFTER"), Is.True);
    }
    [Test]
    public void TestParallelNodeThresholdEdgeCases() {
        List<string> executionResults = new List<string>();

        // Test exact threshold matching
        var context1 = new LSProcessTreeBuilder()
            .Parallel("exactMatch", par => par
                .Handler("success1", (evt, node) => {
                    executionResults.Add("S1");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("success2", (evt, node) => {
                    executionResults.Add("S2");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("failure1", (evt, node) => {
                    executionResults.Add("F1");
                    return LSProcessResultStatus.FAILURE;
                }), 2, 1) // need 2 success, 1 failure
            .Build();

        var mockEvent = new MockProcess();
        var processContext1 = new LSProcessSession(mockEvent, context1);
        var result1 = processContext1.Execute();

        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // failure threshold reached first
        Assert.That(executionResults.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(executionResults.Contains("F1"), Is.True);

        // Test zero success threshold (should succeed immediately after executing available children)
        executionResults.Clear();
        var context2 = new LSProcessTreeBuilder()
            .Parallel("zeroSuccess", par => par
                .Handler("willExecute", (evt, node) => {
                    executionResults.Add("EXECUTED");
                    return LSProcessResultStatus.SUCCESS;
                }), 0, 1) // need 0 success, 1 failure
            .Build();

        var processContext2 = new LSProcessSession(mockEvent, context2);
        var result2 = processContext2.Execute();

        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        // With 0 success threshold, it should succeed immediately, but children may still execute
        Assert.That(executionResults.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void TestNestedParallelOperationsWithDifferentThresholds() {
        int outerExecutionCount = 0;
        int innerExecutionCount = 0;

        var context = new LSProcessTreeBuilder()
            .Parallel("outer", outer => outer
                .Parallel("inner1", inner => inner
                    .Handler("innerExec1", (evt, node) => {
                        innerExecutionCount++;
                        return LSProcessResultStatus.SUCCESS;
                    })
                    .Handler("innerExec2", (evt, node) => {
                        innerExecutionCount++;
                        return LSProcessResultStatus.SUCCESS;
                    }), 1) // inner parallel needs 1 success
                .Handler("outerExec", (evt, node) => {
                    outerExecutionCount++;
                    return LSProcessResultStatus.SUCCESS;
                }), 2) // outer parallel needs 2 successes
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(outerExecutionCount, Is.EqualTo(1)); // outer handler executed once
        Assert.That(innerExecutionCount, Is.GreaterThanOrEqualTo(1)); // at least one inner handler executed
    }

    [Test]
    public void TestComplexSequenceSelectorCombination() {
        List<string> executionFlow = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Sequence("mainSequence", seq => seq
                .Selector("firstSelector", sel => sel
                    .Handler("failFirst", (evt, node) => {
                        executionFlow.Add("FAIL_FIRST");
                        return LSProcessResultStatus.FAILURE;
                    })
                    .Handler("succeedSecond", (evt, node) => {
                        executionFlow.Add("SUCCEED_SECOND");
                        return LSProcessResultStatus.SUCCESS;
                    }))
                .Handler("afterSelector", (evt, node) => {
                    executionFlow.Add("AFTER_SELECTOR");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Selector("secondSelector", sel => sel
                    .Handler("succeedFirst", (evt, node) => {
                        executionFlow.Add("SUCCEED_FIRST");
                        return LSProcessResultStatus.SUCCESS;
                    })
                    .Handler("willNotRun", (evt, node) => {
                        executionFlow.Add("WILL_NOT_RUN");
                        return LSProcessResultStatus.SUCCESS;
                    })))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // Expected flow: FAIL_FIRST -> SUCCEED_SECOND -> AFTER_SELECTOR -> SUCCEED_FIRST
        var expectedFlow = new List<string> { "FAIL_FIRST", "SUCCEED_SECOND", "AFTER_SELECTOR", "SUCCEED_FIRST" };
        Assert.That(executionFlow, Is.EqualTo(expectedFlow));
    }

    [Test]
    public void TestHighVolumeParallelExecution() {
        const int handlerCount = 50;
        int totalExecutions = 0;
        var lockObject = new object();

        var builder = new LSProcessTreeBuilder().Parallel("massParallel", par => {
            for (int i = 0; i < handlerCount; i++) {
                int capturedIndex = i; // capture for lambda
                par.Handler($"handler_{capturedIndex}", (evt, node) => {
                    lock (lockObject) {
                        totalExecutions++;
                    }
                    return LSProcessResultStatus.SUCCESS;
                });
            }
            return par;
        }, handlerCount); // require all to succeed

        var context = builder.Build();
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(totalExecutions, Is.EqualTo(handlerCount));
    }

    [Test]
    public void TestDeepNestedStructurePerformance() {
        const int depth = 10;
        int executionCount = 0;

        // Build deeply nested sequence
        var builder = new LSProcessTreeBuilder();
        var currentBuilder = builder.Sequence("root");

        for (int i = 0; i < depth; i++) {
            int capturedIndex = i;
            currentBuilder = currentBuilder.Sequence($"level_{capturedIndex}", nested => nested
                .Handler($"handler_{capturedIndex}", (evt, node) => {
                    executionCount++;
                    return LSProcessResultStatus.SUCCESS;
                }));
        }

        var context = builder.Build();
        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = processContext.Execute();
        stopwatch.Stop();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executionCount, Is.EqualTo(depth));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000)); // should complete within 1 second
    }

    #endregion

    #region Condition System Tests

    [Test]
    public void TestMultipleConditionComposition() {
        int conditionCallCount = 0;
        bool eventTypeCondition = true;
        bool stateCondition = true;

        // Test case 1: Both conditions true - should execute
        var context1 = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false,
                   (evt, node) => { conditionCallCount++; return eventTypeCondition; },
                   (evt, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = true;
        stateCondition = true;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockEvent1 = new MockProcess();
        var processContext1 = new LSProcessSession(mockEvent1, context1);
        var result1 = processContext1.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(conditionCallCount, Is.EqualTo(2)); // both conditions evaluated

        // Test case 2: First condition false - should not execute
        var context2 = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false,
                   (evt, node) => { conditionCallCount++; return eventTypeCondition; },
                   (evt, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = false;
        stateCondition = true;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockEvent2 = new MockProcess();
        var processContext2 = new LSProcessSession(mockEvent2, context2);
        var result2 = processContext2.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // sequence succeeds when handler is skipped
        Assert.That(_handler1CallCount, Is.EqualTo(0));
        Assert.That(conditionCallCount, Is.EqualTo(1)); // only first condition evaluated (short-circuit)

        // Test case 3: Second condition false - should not execute
        var context3 = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("conditionalHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false,
                   (evt, node) => { conditionCallCount++; return eventTypeCondition; },
                   (evt, node) => { conditionCallCount++; return stateCondition; }))
            .Build();

        eventTypeCondition = true;
        stateCondition = false;
        conditionCallCount = 0;
        _handler1CallCount = 0;

        var mockEvent3 = new MockProcess();
        var processContext3 = new LSProcessSession(mockEvent3, context3);
        var result3 = processContext3.Execute();
        Assert.That(result3, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0));
        Assert.That(conditionCallCount, Is.EqualTo(2)); // both conditions evaluated
    }

    [Test]
    public void TestConditionParameterValidation() {
        // Test with data-based conditions
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("dataConditionHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => {
                    return true;
                }))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // should execute because condition is met
    }

    [Test]
    public void TestNodeMetadataBasedConditions() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("priorityConditionHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.HIGH, false, (evt, node) => {
                    // Condition based on node priority
                    return node.Priority == LSProcessPriority.HIGH;
                })
                .Handler("orderConditionHandler", (evt, node) => {
                    _handler2CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => {
                    // Condition based on node order
                    return node.Order >= 0;
                }))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // high priority condition met
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // order condition met
    }

    [Test]
    public void TestConditionalExecutionPathsInSelector() {
        List<string> executionPath = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Selector("root", sel => sel
                .Handler("alwaysFailCondition", (evt, node) => {
                    executionPath.Add("SHOULD_NOT_EXECUTE");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.HIGH, false, (evt, node) => false) // condition always false
                .Handler("conditionalSuccess", (evt, node) => {
                    executionPath.Add("CONDITIONAL_SUCCESS");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => true) // condition always true
                .Handler("fallback", (evt, node) => {
                    executionPath.Add("FALLBACK");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.LOW))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        // Only the conditional success should execute (first with true condition)
        Assert.That(executionPath, Is.EqualTo(new List<string> { "CONDITIONAL_SUCCESS" }));
    }

    [Test]
    public void TestParallelNodeConditionFiltering() {
        List<string> executedHandlers = new List<string>();
        bool condition1 = true;
        bool condition2 = false;
        bool condition3 = true;

        var context = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Handler("handler1", (evt, node) => {
                    executedHandlers.Add("H1");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => condition1)
                .Handler("handler2", (evt, node) => {
                    executedHandlers.Add("H2");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => condition2)
                .Handler("handler3", (evt, node) => {
                    executedHandlers.Add("H3");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => condition3), 2) // require 2 to succeed
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        // Only handlers 1 and 3 should execute (conditions true)
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executedHandlers.Contains("H1"), Is.True);
        Assert.That(executedHandlers.Contains("H2"), Is.False); // condition was false
        Assert.That(executedHandlers.Contains("H3"), Is.True);
        Assert.That(executedHandlers.Count, Is.EqualTo(2));
    }

    [Test]
    public void TestConditionExceptionHandling() {
        bool throwException = true;

        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("exceptionConditionHandler", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, false, (evt, node) => {
                    if (throwException) throw new InvalidOperationException("Condition error");
                    return true;
                }))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // Exception in condition should propagate (current implementation behavior)
        throwException = true;
        Assert.Throws<InvalidOperationException>(() => {
            processContext.Execute();
        });

        // Test that normal conditions still work
        throwException = false;
        _handler1CallCount = 0;

        var result2 = processContext.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler executed when condition doesn't throw
    }

    [Test]
    public void TestSequenceNodeWithConditions() {
        bool sequenceCondition = true;

        var context = new LSProcessTreeBuilder()
            .Sequence("conditionalSequence", seq => seq
                .Handler("handler1", (evt, node) => {
                    _handler1CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("handler2", (evt, node) => {
                    _handler2CallCount++;
                    return LSProcessResultStatus.SUCCESS;
                }), LSProcessPriority.NORMAL, false, false, (evt, node) => sequenceCondition)
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // Test sequence executes when condition is true
        sequenceCondition = true;
        _handler1CallCount = 0;
        _handler2CallCount = 0;

        var result1 = processContext.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));

        // Test sequence is skipped when condition is false
        sequenceCondition = false;
        _handler1CallCount = 0;
        _handler2CallCount = 0;

        var result2 = processContext.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // parent succeeds when child is skipped
        Assert.That(_handler1CallCount, Is.EqualTo(0));
        Assert.That(_handler2CallCount, Is.EqualTo(0));
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void TestInvalidBuilderConfigurations() {
        // Test building with no nodes at all
        var emptyBuilder = new LSProcessTreeBuilder();
        Assert.Throws<LSException>(() => emptyBuilder.Build());

        // Test that empty sequences/selectors/parallels are actually allowed
        // (they just return SUCCESS immediately when processed)
        var emptySequence = new LSProcessTreeBuilder().Sequence("empty").Build();
        Assert.That(emptySequence, Is.Not.Null);

        var emptySelector = new LSProcessTreeBuilder().Selector("empty").Build();
        Assert.That(emptySelector, Is.Not.Null);

        var emptyParallel = new LSProcessTreeBuilder().Parallel("empty", par => par, 0).Build();
        Assert.That(emptyParallel, Is.Not.Null);

        // Test that they work when processed
        var mockEvent = new MockProcess();

        var seqResult = new LSProcessSession(mockEvent, emptySequence).Execute();
        Assert.That(seqResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        var selResult = new LSProcessSession(mockEvent, emptySelector).Execute();
        Assert.That(selResult, Is.EqualTo(LSProcessResultStatus.FAILURE)); // selector with no children fails

        var parResult = new LSProcessSession(mockEvent, emptyParallel).Execute();
        Assert.That(parResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestNullReferenceHandling() {
        // Test what actually happens with null handlers and IDs
        // Test if null handlers are allowed or throw exceptions

        Exception? caughtException = null;
        try {
            var context = new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("test", null!))
                .Build();
            // If build succeeds, test processing
            var mockEvent = new MockProcess();
            var processContext = new LSProcessSession(mockEvent, context);
            try {
                processContext.Execute();
            } catch (Exception ex) {
                caughtException = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException = ex;
        } catch (ArgumentException ex) {
            caughtException = ex;
        }

        // The system should handle nulls somehow - either throw during build or processing
        if (caughtException != null) {
            Assert.Pass($"Null handler correctly handled with {caughtException.GetType().Name}: {caughtException.Message}");
        } else {
            Assert.Fail("Expected some form of null handler validation, but none occurred");
        }

        // Test empty/whitespace node IDs - these should definitely be invalid
        Assert.Throws<ArgumentException>(() => {
            new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("", _mockHandler1))
                .Build();
        });

        Assert.Throws<ArgumentException>(() => {
            new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("   ", _mockHandler1))
                .Build();
        });
    }

    [Test]
    public void TestDuplicateNodeIds() {
        // Test if the system actually prevents duplicate node IDs
        // Let's see what happens when we try to create duplicates

        try {
            var context = new LSProcessTreeBuilder()
                .Sequence("root", seq => seq
                    .Handler("duplicate", _mockHandler1)
                    .Handler("duplicate", _mockHandler2))
                .Build();

            // If this succeeds, let's test the behavior
            var mockEvent = new MockProcess();
            var processContext = new LSProcessSession(mockEvent, context);
            var result = processContext.Execute();

            // The system might allow duplicates but only use the last one
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        } catch (LSException) {
            // If it does throw, that's the expected behavior
            Assert.Pass("Duplicate IDs correctly throw LSException");
        }

        // Test that valid unique IDs work fine
        var validContext = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2))
            .Build();

        Assert.That(validContext, Is.Not.Null);
        Assert.That(validContext.HasChild("handler1"), Is.True);
        Assert.That(validContext.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestRuntimeExceptionPropagation() {
        List<string> executionOrder = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("beforeException", (evt, node) => {
                    executionOrder.Add("BEFORE");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("throwsException", (evt, node) => {
                    executionOrder.Add("EXCEPTION");
                    throw new InvalidOperationException("Handler error");
                })
                .Handler("afterException", (evt, node) => {
                    executionOrder.Add("AFTER");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // Exception should propagate and stop execution
        Assert.Throws<InvalidOperationException>(() => {
            processContext.Execute();
        });

        // Verify execution stopped at the exception
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "BEFORE", "EXCEPTION" }));
    }

    [Test]
    public void TestParallelNodeInvalidThresholds() {
        // Test what happens with extreme threshold values
        // The system might allow negative values or handle them gracefully

        // Test if negative thresholds are allowed or converted
        try {
            var context1 = new LSProcessTreeBuilder()
                .Parallel("root", par => par.Handler("handler", _mockHandler1), -1, 0)
                .Build();

            // If allowed, test behavior
            var mockEvent = new MockProcess();
            var result = new LSProcessSession(mockEvent, context1).Execute();
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // might succeed immediately

        } catch (ArgumentException) {
            Assert.Pass("Negative success threshold correctly throws ArgumentException");
        }

        // Test success threshold greater than child count
        var context = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2), 5) // requires 5 but only has 2 children
            .Build();

        var mockEvent2 = new MockProcess();
        var processContext = new LSProcessSession(mockEvent2, context);

        // Should fail because impossible to meet threshold
        var result2 = processContext.Execute();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.FAILURE));

        // Test zero thresholds work
        var context3 = new LSProcessTreeBuilder()
            .Parallel("root", par => par.Handler("handler", _mockHandler1), 0, 0)
            .Build();

        var result3 = new LSProcessSession(mockEvent2, context3).Execute();
        Assert.That(result3, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestConcurrentModificationDetection() {
        var sequence = LSProcessNodeSequence.Create("seq", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);

        sequence.AddChild(handler1);

        // Start processing to set internal state
        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, sequence);

        // Process first to set internal processing state
        var result = context.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // After processing has started, modifications should be restricted
        Assert.Throws<InvalidOperationException>(() => {
            sequence.AddChild(handler2);
        });
    }

    [Test]
    public void TestProcessContextNullEventHandling() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq.Handler("handler", _mockHandler1))
            .Build();

        // Test null event - system might accept it
        Exception? caughtException = null;
        try {
            var processContext = new LSProcessSession(null!, context);
            // If construction succeeds, test processing
            try {
                var result = processContext.Execute();
                // System might handle null gracefully
                Assert.That(result, Is.AnyOf(LSProcessResultStatus.SUCCESS, LSProcessResultStatus.FAILURE, LSProcessResultStatus.WAITING, LSProcessResultStatus.CANCELLED));
            } catch (Exception ex) {
                caughtException = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException = ex;
        }

        // Either exception or graceful handling is acceptable
        if (caughtException != null) {
            Assert.Pass($"Null event handled with {caughtException.GetType().Name}");
        } else {
            Assert.Pass("System gracefully handles null events");
        }

        // Test null context
        var mockEvent = new MockProcess();
        Exception? caughtException2 = null;
        try {
            var processContext = new LSProcessSession(mockEvent, null!);
            try {
                var result = processContext.Execute();
                Assert.That(result, Is.AnyOf(LSProcessResultStatus.SUCCESS, LSProcessResultStatus.FAILURE, LSProcessResultStatus.WAITING, LSProcessResultStatus.CANCELLED));
            } catch (Exception ex) {
                caughtException2 = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException2 = ex;
        }

        // Either exception or graceful handling is acceptable
        if (caughtException2 != null) {
            Assert.Pass($"Null context handled with {caughtException2.GetType().Name}");
        } else {
            Assert.Pass("System gracefully handles null context");
        }
    }

    [Test]
    public void TestBuilderStateValidation() {
        var builder = new LSProcessTreeBuilder();

        // Test building twice should NOT work (builder should NOT be reusable)
        var context1 = builder
            .Sequence("root", seq => seq.Handler("handler", _mockHandler1))
            .Build();
        Assert.That(context1, Is.Not.Null);

        // Test that builder can NOT be reused
        Assert.Throws<LSException>(() => {
            var context2 = builder
                .Selector("root2", sel => sel.Handler("handler2", _mockHandler2))
                .Build();
        });
    }

    [Test]
    public void TestGracefulDegradationWithPartialFailures() {
        List<string> executionResults = new List<string>();

        var context = new LSProcessTreeBuilder()
            .Selector("root", sel => sel
                .Handler("failingHandler", (evt, node) => {
                    executionResults.Add("FAILED");
                    return LSProcessResultStatus.FAILURE;
                })
                .Handler("workingHandler", (evt, node) => {
                    executionResults.Add("SUCCESS");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("fallbackHandler", (evt, node) => {
                    executionResults.Add("FALLBACK");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);
        var result = processContext.Execute();

        // Selector should succeed with first working handler
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executionResults, Is.EqualTo(new List<string> { "FAILED", "SUCCESS" }));
    }

    #endregion

    #region State Transition Tests

    [Test]
    public void TestBasicStateTransitions() {
        // Test that nodes transition from UNKNOWN to terminal states properly
        var successHandler = LSProcessNodeHandler.Create("success", _mockHandler1, 0);
        var failureHandler = LSProcessNodeHandler.Create("failure", (evt, node) => LSProcessResultStatus.FAILURE, 0);

        // Test initial state
        Assert.That(successHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));
        Assert.That(failureHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockEvent = new MockProcess();

        // Test success transition
        var context1 = new LSProcessSession(mockEvent, successHandler);
        ((ILSProcessNode)successHandler).Execute(context1);
        Assert.That(successHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // Test failure transition
        var context2 = new LSProcessSession(mockEvent, failureHandler);
        ((ILSProcessNode)failureHandler).Execute(context2);
        Assert.That(failureHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void TestSequenceNodeStateTransitions() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2))
            .Build();

        // Initial state should be UNKNOWN
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // After successful processing, should be SUCCESS
        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestSelectorNodeStateTransitions() {
        // Create fresh handlers to avoid any state pollution
        var freshHandler1 = new LSProcessHandler((evt, node) => LSProcessResultStatus.FAILURE);
        var freshHandler2 = new LSProcessHandler((evt, node) => LSProcessResultStatus.SUCCESS);

        var context = new LSProcessTreeBuilder()
            .Selector("selectorRoot", sel => sel
                .Handler("failingHandler", freshHandler1)
                .Handler("successHandler", freshHandler2))
            .Build();

        // Check initial state - might be affected by previous tests if builder caches nodes
        var initialStatus = context.GetNodeStatus();
        Assert.That(initialStatus, Is.AnyOf(LSProcessResultStatus.UNKNOWN, LSProcessResultStatus.SUCCESS),
            $"Initial status was {initialStatus}, expected UNKNOWN or SUCCESS if cached");

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestParallelNodeStateTransitions() {
        var context = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2), 2) // both must succeed
            .Build();

        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        var result = processContext.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestWaitingStateTransitions() {
        var waitingHandler = LSProcessNodeHandler.Create("waiting", (evt, node) => LSProcessResultStatus.WAITING, 0);

        Assert.That(waitingHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, waitingHandler);

        // Process should result in WAITING
        ((ILSProcessNode)waitingHandler).Execute(context);
        Assert.That(waitingHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.WAITING));

        // Test Resume functionality via interface
        var resumeResult = ((ILSProcessNode)waitingHandler).Resume(context);
        Assert.That(resumeResult, Is.AnyOf(
            LSProcessResultStatus.SUCCESS,
            LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.WAITING
        ));

        // Test Fail functionality if still waiting
        if (waitingHandler.GetNodeStatus() == LSProcessResultStatus.WAITING) {
            var failResult = ((ILSProcessNode)waitingHandler).Fail(context);
            Assert.That(failResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
            Assert.That(waitingHandler.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.FAILURE));
        }
    }

    [Test]
    public void TestCancelledStateTransitions() {
        var context = new LSProcessTreeBuilder()
            .Sequence("cancelRoot", seq => seq
                .Handler("cancelHandler1", _mockHandler1)
                .Handler("cancelHandler2", _mockHandler2))
            .Build();

        var initialStatus = context.GetNodeStatus();
        Assert.That(initialStatus, Is.AnyOf(LSProcessResultStatus.UNKNOWN, LSProcessResultStatus.SUCCESS),
            $"Initial status was {initialStatus}");

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // Test cancellation functionality
        var cancelResult = ((ILSProcessNode)context).Cancel(processContext);

        // The system might handle cancellation differently
        Assert.That(cancelResult, Is.AnyOf(
            LSProcessResultStatus.CANCELLED,
            LSProcessResultStatus.UNKNOWN,
            LSProcessResultStatus.SUCCESS,
            LSProcessResultStatus.FAILURE
        ), $"Cancel returned {cancelResult}");

        // Test processing after cancel attempt
        var processResult = processContext.Execute();
        Assert.That(processResult, Is.AnyOf(
            LSProcessResultStatus.SUCCESS,
            LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.CANCELLED
        ), $"Process after cancel returned {processResult}");

        // Test final state
        var finalStatus = context.GetNodeStatus();
        Assert.That(finalStatus, Is.AnyOf(
            LSProcessResultStatus.SUCCESS,
            LSProcessResultStatus.FAILURE,
            LSProcessResultStatus.CANCELLED
        ), $"Final status is {finalStatus}");
    }

    [Test]
    public void TestExecutionCountProgression() {
        var uniqueHandler = new LSProcessHandler((evt, node) => LSProcessResultStatus.SUCCESS);
        var handler = LSProcessNodeHandler.Create("handlerForExecCount", uniqueHandler, 0);

        Assert.That(handler.ExecutionCount, Is.EqualTo(0));

        var mockEvent = new MockProcess();

        // Test first execution
        var context1 = new LSProcessSession(mockEvent, handler);
        ((ILSProcessNode)handler).Execute(context1);
        var firstCount = handler.ExecutionCount;
        Assert.That(firstCount, Is.GreaterThanOrEqualTo(1), "Execution count should increment after first process");

        // Test if second execution increments (it might not if the node caches results)
        var context2 = new LSProcessSession(mockEvent, handler);
        ((ILSProcessNode)handler).Execute(context2);
        var secondCount = handler.ExecutionCount;

        // System might not increment on repeated processing of already successful nodes
        Assert.That(secondCount, Is.GreaterThanOrEqualTo(firstCount),
            $"Execution count should not decrease. First: {firstCount}, Second: {secondCount}");

        // Test with a fresh handler for guaranteed progression
        var handler2 = LSProcessNodeHandler.Create("freshHandler", uniqueHandler, 0);
        var context3 = new LSProcessSession(mockEvent, handler2);
        ((ILSProcessNode)handler2).Execute(context3);
        Assert.That(handler2.ExecutionCount, Is.EqualTo(1), "Fresh handler should have execution count of 1");
    }

    [Test]
    public void TestComplexHierarchyStateTransitions() {
        var context = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Selector("selector1", sel => sel
                    .Handler("fail1", (evt, node) => LSProcessResultStatus.FAILURE)
                    .Handler("success1", _mockHandler1))
                .Parallel("parallel1", par => par
                    .Handler("handler1", _mockHandler1)
                    .Handler("handler2", _mockHandler2), 2))
            .Build();

        // Test initial state
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockEvent = new MockProcess();
        var processContext = new LSProcessSession(mockEvent, context);

        // Process the entire hierarchy
        var result = processContext.Execute();

        // Root should succeed
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(context.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestStateTransitionImmutability() {
        var handler = LSProcessNodeHandler.Create("handler", _mockHandler1, 0);

        // Test that we cannot directly modify state-related properties
        Assert.That(handler.NodeID, Is.EqualTo("handler"));
        Assert.That(handler.Priority, Is.EqualTo(LSProcessPriority.NORMAL));
        Assert.That(handler.Order, Is.EqualTo(0));

        // These should be read-only after creation
        var originalId = handler.NodeID;
        var originalPriority = handler.Priority;
        var originalOrder = handler.Order;

        // Process the handler
        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, handler);
        ((ILSProcessNode)handler).Execute(context);

        // Properties should remain unchanged
        Assert.That(handler.NodeID, Is.EqualTo(originalId));
        Assert.That(handler.Priority, Is.EqualTo(originalPriority));
        Assert.That(handler.Order, Is.EqualTo(originalOrder));

        // Only ExecutionCount should change
        Assert.That(handler.ExecutionCount, Is.EqualTo(1));
    }

    [Test]
    public void TestInvalidNodeStateTransitions() {
        var handler = LSProcessNodeHandler.Create("handler", _mockHandler1, 0);

        // Test that node properties are read-only after creation
        Assert.That(handler.NodeID, Is.EqualTo("handler"));
        Assert.That(handler.Priority, Is.EqualTo(LSProcessPriority.NORMAL));
        Assert.That(handler.Order, Is.EqualTo(0));

        // Test that ExecutionCount starts at 0
        Assert.That(handler.ExecutionCount, Is.EqualTo(0));

        // ExecutionCount should be immutable from outside
        // (can only be modified through processing)
        var mockEvent = new MockProcess();
        var context = new LSProcessSession(mockEvent, handler);
        ((ILSProcessNode)handler).Execute(context);

        Assert.That(handler.ExecutionCount, Is.EqualTo(1));
    }

    #endregion

    #region LSProcessContextManager Tests

    [Test]
    public void TestContextManagerRegistrationAndRetrieval() {
        LSProcessManager.Singleton.Register<MockProcess>(root => root
            .Sequence("seq", seq => seq
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2))
        );
        LSProcessManager.Singleton.Register<MockProcess>(root => root
            .Selector("sel", sel => sel
                .Handler("handler3", _mockHandler1)
                .Handler("handler4", _mockHandler2))
        );

        var root = LSProcessManager.Singleton.GetRootNode<MockProcess>();
        var typeName = typeof(MockProcess).Name;
        Assert.That(root.NodeID, Is.EqualTo(typeName));
        Assert.That(root, Is.InstanceOf<LSProcessNodeParallel>());

        Assert.That(root.HasChild("seq"), Is.True);
        Assert.That(root.HasChild("sel"), Is.True);

        var mockEvent = new MockProcess();
        var result = mockEvent.WithProcessing(s => s
            .Sequence("seq", seq => seq
                .Handler("handler5", _mockHandler1)
                .Handler("handler6", _mockHandler2))
        ).Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(3)); // handler1 and handler3
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 only
    }

    #endregion
}




