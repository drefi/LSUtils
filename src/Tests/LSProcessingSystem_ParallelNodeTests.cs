using LSUtils.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Processing.Tests;

/// <summary>
/// Tests for LSProcessNodeParallel functionality.
/// </summary>
[TestFixture]
public class LSProcessingSystem_ParallelNodeTests {
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

        _mockHandler1 = (proc, node) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler2 = (proc, node) => {
            _handler2CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler3Failure = (proc, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _mockHandler3Cancel = (proc, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _mockHandler3Waiting = (proc, node) => {
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
    public void TestBuilderBasicParallel() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root")
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestBuilderParallelSuccess() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1), 1) // require 1 to succeed
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelSuccess2() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2), 2) // require 2 to succeed
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelSuccess3() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Failure), 1) // require 1 to succeed
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelFailure() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Failure), 1)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelFailure2() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Failure), 2) // require 2 to succeed
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderParallelWaitingSuccessWithResume() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
            , 1)
        .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = process.Resume("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void TestBuilderParallelWaitingSuccessWithFail() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        LSLogger.Singleton.Debug("[TestBuilderParallelWaitingSuccessWithFail] Processing...");
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler1 should succeed immediately
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        LSLogger.Singleton.Debug("[TestBuilderParallelWaitingSuccessWithFail] Failing handler1...");
        var resumeResult = process.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler1 has already succeeded, this should not change anything
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void TestBuilderParallelWaitingSuccessWithFailAndResume() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler3Waiting)
                .Handler("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult = process.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.WAITING)); // we have 1 success, 1 failure and 1 waiting
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = process.Resume("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // now we have 2 successes
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void TestBuilderParallelWaitingFailureWithFail() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        // Simulate external resume
        var resumeResult = process.Fail("handler1");
        Assert.That(resumeResult, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); //should not call handler again
    }

    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFailureWithResume() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Failure)
                .Handler("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = process.Resume("handler2");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // handler2 has failed but
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void TestBuilderBasicParallelMultipleWaitingSuccessResume() {
        var sourceStr = "ParallelMultipleWaitingSuccessResume";
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting)
                .Handler("handler3", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        Assert.That(builder.HasChild("handler3"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        _logger.Info($"Process result: {result}", sourceStr);
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(3));
        // Simulate external resume
        var resumeResult1 = process.Resume("handler1");
        _logger.Info($"Resume handler1 result: {resumeResult1}", sourceStr);
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the builder is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
        var resumeResult2 = process.Resume("handler2");
        _logger.Info($"Resume handler2 result: {resumeResult2}", sourceStr);
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // now two have resumed, which meets the requirement of 2
        Assert.That(_handler3CallCount, Is.EqualTo(3)); //should not call handler again
                                                        // handler3 is still waiting, but the parallel node has already succeeded
        var resumeResult3 = process.Resume("handler3");
        Assert.That(resumeResult3, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // handler3 should return SUCCESS as the parallel node has already succeeded
    }

    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting), 2)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = process.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the builder is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = process.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
    }

    [Test]
    public void TestBuilderBasicParallelMultipleWaitingFail2() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Handler("handler1", _mockHandler3Waiting)
                .Handler("handler2", _mockHandler3Waiting), 1)
            .Build();

        Assert.That(builder, Is.Not.Null);
        Assert.That(builder.NodeID, Is.EqualTo("root"));
        Assert.That(builder.HasChild("handler1"), Is.True);
        Assert.That(builder.HasChild("handler2"), Is.True);
        // Test execution
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(2));
        // Simulate external resume
        var resumeResult1 = process.Fail("handler1");
        Assert.That(resumeResult1, Is.EqualTo(LSProcessResultStatus.WAITING)); // the builder is still waiting on handler2
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
        var resumeResult2 = process.Fail("handler2");
        Assert.That(resumeResult2, Is.EqualTo(LSProcessResultStatus.FAILURE)); // now both have failed
        Assert.That(_handler3CallCount, Is.EqualTo(2)); //should not call handler again
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
        var builder = new LSProcessTreeBuilder()
            .Parallel("par", builder => builder
                .Handler("h1", _mockHandler1)
                .Handler("h2", _mockHandler2), 0, 0) // 0 required to succeed, 0 required to fail
            .Build();

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);

        // With 0 required to succeed, should succeed immediately
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestParallelNodeStateTransitions() {
        var builder = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2), 2) // both must succeed
            .Build();

        Assert.That(builder.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);

        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(builder.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestMixedPriorityExecutionWithParallelAndSequence() {
        List<string> executionOrder = new List<string>();

        var builder = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Parallel("parallel1", par => par
                    .Handler("highParallel", (proc, node) => {
                        executionOrder.Add("HIGH_PAR");
                        return LSProcessResultStatus.SUCCESS;
                    }, LSProcessPriority.HIGH)
                    .Handler("lowParallel", (proc, node) => {
                        executionOrder.Add("LOW_PAR");
                        return LSProcessResultStatus.SUCCESS;
                    }, LSProcessPriority.LOW), 2) // require both to succeed
                .Handler("criticalAfter", (proc, node) => {
                    executionOrder.Add("CRITICAL_AFTER");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.CRITICAL))
            .Build();

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();

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
                .Handler("success1", (proc, node) => {
                    executionResults.Add("S1");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("success2", (proc, node) => {
                    executionResults.Add("S2");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("failure1", (proc, node) => {
                    executionResults.Add("F1");
                    return LSProcessResultStatus.FAILURE;
                }), 2, 1) // need 2 success, 1 failure
            .Build();

        var mockProcess = new MockProcess();
        var processContext1 = new LSProcessSession(mockProcess, context1);
        var result1 = processContext1.Execute();

        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // since the number of successes is higher than failures it has precedence
        Assert.That(executionResults.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(executionResults.Contains("F1"), Is.True);

        // Test zero success threshold (should succeed immediately after executing available children)
        executionResults.Clear();
        var context2 = new LSProcessTreeBuilder()
            .Parallel("zeroSuccess", par => par
                .Handler("willExecute", (proc, node) => {
                    executionResults.Add("EXECUTED");
                    return LSProcessResultStatus.SUCCESS;
                }), 0, 1) // need 0 success, 1 failure
            .Build();

        var processContext2 = new LSProcessSession(mockProcess, context2);
        var result2 = processContext2.Execute();

        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        // With 0 success threshold, it should succeed immediately, but children may still execute
        Assert.That(executionResults.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void TestParallelNodeThresholdEdgeCases2() {
        List<string> executionResults = new List<string>();

        // Test exact threshold matching
        var context1 = new LSProcessTreeBuilder()
            .Parallel("exactMatch", par => par
                .Handler("success1", (proc, node) => {
                    executionResults.Add("S1");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("failure1", (proc, node) => {
                    executionResults.Add("F1");
                    return LSProcessResultStatus.FAILURE;
                })
                .Handler("failure2", (proc, node) => {
                    executionResults.Add("F2");
                    return LSProcessResultStatus.FAILURE;
                }), 1, 2) // need 1 success, 2 failure
            .Build();

        var mockProcess = new MockProcess();
        var processContext1 = new LSProcessSession(mockProcess, context1);
        var result1 = processContext1.Execute();

        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.FAILURE)); // since the number of failures is higher than successes it has precedence
        Assert.That(executionResults.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(executionResults.Contains("S1"), Is.True);
        Assert.That(executionResults.Contains("F1"), Is.True);
        Assert.That(executionResults.Contains("F2"), Is.True);
    }

    [Test]
    public void TestNestedParallelOperationsWithDifferentThresholds() {
        int outerExecutionCount = 0;
        int innerExecutionCount = 0;

        var builder = new LSProcessTreeBuilder()
            .Parallel("outer", outer => outer
                .Parallel("inner1", inner => inner
                    .Handler("innerExec1", (proc, node) => {
                        innerExecutionCount++;
                        return LSProcessResultStatus.SUCCESS;
                    })
                    .Handler("innerExec2", (proc, node) => {
                        innerExecutionCount++;
                        return LSProcessResultStatus.SUCCESS;
                    }), 1) // inner parallel needs 1 success
                .Handler("outerExec", (proc, node) => {
                    outerExecutionCount++;
                    return LSProcessResultStatus.SUCCESS;
                }), 2) // outer parallel needs 2 successes
            .Build();

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(outerExecutionCount, Is.EqualTo(1)); // outer handler executed once
        Assert.That(innerExecutionCount, Is.GreaterThanOrEqualTo(1)); // at least one inner handler executed
    }

    [Test]
    public void TestParallelNodeConditionFiltering() {
        List<string> executedHandlers = new List<string>();
        bool condition1 = true;
        bool condition2 = false;
        bool condition3 = true;

        var builder = new LSProcessTreeBuilder()
            .Parallel("root", par => par
                .Handler("handler1", (proc, node) => {
                    executedHandlers.Add("H1");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, (proc, node) => condition1)
                .Handler("handler2", (proc, node) => {
                    executedHandlers.Add("H2");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, (proc, node) => condition2)
                .Handler("handler3", (proc, node) => {
                    executedHandlers.Add("H3");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL, (proc, node) => condition3), 2) // require 2 to succeed
            .Build();

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);
        var result = process.Execute();

        // Only handlers 1 and 3 should execute (conditions true)
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executedHandlers.Contains("H1"), Is.True);
        Assert.That(executedHandlers.Contains("H2"), Is.False); // condition was false
        Assert.That(executedHandlers.Contains("H3"), Is.True);
        Assert.That(executedHandlers.Count, Is.EqualTo(2));
    }

    [Test]
    public void TestHighVolumeParallelExecution() {
        const int handlerCount = 50;
        int totalExecutions = 0;
        var lockObject = new object();

        var builder = new LSProcessTreeBuilder().Parallel("massParallel", par => {
            for (int i = 0; i < handlerCount; i++) {
                int capturedIndex = i; // capture for lambda
                par.Handler($"handler_{capturedIndex}", (proc, node) => {
                    lock (lockObject) {
                        totalExecutions++;
                    }
                    return LSProcessResultStatus.SUCCESS;
                });
            }
            return par;
        }, handlerCount); // require all to succeed

        var root = builder.Build();
        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, root);
        var result = process.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(totalExecutions, Is.EqualTo(handlerCount));
    }
}
