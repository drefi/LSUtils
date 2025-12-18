namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;
[TestFixture]
public class TreeBuilder_Tests {
    public class MockProcess : LSProcess {
        public MockProcess() { }
    }
    //mock process with processing override
    public class ProcessingMockProcess : LSProcess {
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
            return builder.Handler("myHandler", session => {
                LSLogger.Singleton.Info("processing is working", source: (nameof(ProcessingMockProcess), true));
                return LSProcessResultStatus.SUCCESS;
            });
        }
    }

    private LSProcessHandler _mockHandler1 = null!;
    private LSProcessHandler _mockHandler2 = null!;
    private LSProcessHandler _mockHandler3Failure = null!;
    private LSProcessHandler _mockHandler3Cancel = null!;
    private LSProcessHandler _mockHandler3Waiting = null!;
    private int _handler1CallCount;
    private int _handler2CallCount;
    private int _handler3CallCount;
    private LSLogger _logger = null!;

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
    public void TestBuildEmptyBuilder() {
        var builder = new LSProcessTreeBuilder();
        Assert.Throws<LSException>(() => builder.Build());
    }

    [Test]
    public void TestRemoveNonExistentNode() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root");

        // Should not throw, just return false or handle gracefully
        Assert.DoesNotThrow(() => builder.RemoveChild("nonExistent"));
    }

    [Test]
    public void TestRemoveChildScenarios() {
        var root = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Handler("handler1", _mockHandler1)
                .Sequence("childSeq", child => child
                    .Handler("handler2", _mockHandler2)))
            .Build();

        var builder = new LSProcessTreeBuilder(root);
        builder.RemoveChild("handler1");
        builder.RemoveChild("childSeq");

        var updatedRoot = builder.Build();
        Assert.That(updatedRoot.HasChild("handler1"), Is.False);
        Assert.That(updatedRoot.HasChild("childSeq"), Is.False);
        Assert.That(updatedRoot.GetChildren().Length, Is.EqualTo(0));
    }

    [Test]
    public void TestNodeCloneIndependence() {
        // Test sequence node cloning
        var root = new LSProcessTreeBuilder()
            .Sequence("seq", seq => seq.Handler("handler", _mockHandler1))
            .Build();

        var clonedRoot = root.Clone();

        // Should be different instances
        Assert.That(clonedRoot, Is.Not.SameAs(root));
        Assert.That(clonedRoot.NodeID, Is.EqualTo(root.NodeID));

    }

    [Test]
    public void TestNodePriorityAndOrderSorting() {
        List<string> executionOrder = new List<string>();

        var builder = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Handler("lowPriority", (session) => {
                    executionOrder.Add("LOW");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.LOW)
                .Handler("normalPriority", (session) => {
                    executionOrder.Add("NORMAL");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.NORMAL)
                .Handler("highPriority", (session) => {
                    executionOrder.Add("HIGH");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.HIGH)
                .Handler("criticalPriority", (session) => {
                    executionOrder.Add("CRITICAL");
                    return LSProcessResultStatus.SUCCESS;
                }, LSProcessPriority.CRITICAL))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, builder);
        var result = session.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // Should execute in priority order: CRITICAL, HIGH, NORMAL, LOW
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "CRITICAL", "HIGH", "NORMAL", "LOW" }));
    }

    [Test]
    public void TestExecutionPreserveOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        var seqA = new LSProcessTreeBuilder()
            .Sequence("seqA", seq => seq
                .Handler("handlerA", (session) => {
                    executionOrder.Add("A");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var seqB = new LSProcessTreeBuilder()
            .Sequence("seqB", seq => seq
                .Handler("handlerB", (session) => {
                    executionOrder.Add("B");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var merged = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(seqA)
                .Merge(seqB))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, merged);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // in the same priority order should be preserved
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "A", "B" }));
    }

    public void TestExecutionPriorityOrderAfterMerge() {
        List<string> executionOrder = new List<string>();
        var seqA = new LSProcessTreeBuilder()
            .Sequence("seqA", seq => seq
                .Handler("handlerA", (session) => {
                    executionOrder.Add("A");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var seqB = new LSProcessTreeBuilder()
            .Sequence("seqB", seq => seq
                .Handler("handlerB", (session) => {
                    executionOrder.Add("B");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var seqC = new LSProcessTreeBuilder()
            .Sequence("seqC", seq => seq
                .Handler("handlerC", (session) => {
                    executionOrder.Add("C");
                    return LSProcessResultStatus.SUCCESS;
                }), LSProcessPriority.HIGH)
            .Build();
        var mergedContextWithPriority = new LSProcessTreeBuilder()
            .Sequence("root", root => root
                .Merge(seqA)
                .Merge(seqC) // C has higher priority
                .Merge(seqB))
            .Build();

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, mergedContextWithPriority);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        Assert.That(executionOrder, Is.EqualTo(new List<string> { "C", "A", "B" }));
    }

    // Test ProcessingMockProcess
    [Test]
    public void Test_ProcessingMockProcess() {
        var process = new ProcessingMockProcess();
        process.Execute();
    }
}
