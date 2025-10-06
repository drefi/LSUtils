using LSUtils.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Processing.Tests;

/// <summary>
/// Complex integration tests with nested structures and advanced scenarios.
/// </summary>
[TestFixture]
public class LSProcessingSystem_ComplexIntegrationTests {
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
    public void TestBuilderNestedStructure() {
        var root = new LSProcessTreeBuilder()
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

        Assert.That(root.NodeID, Is.EqualTo("root"));
        Assert.That(root.HasChild("childSequence"), Is.True);
        Assert.That(root.HasChild("childSelector"), Is.True);
        Assert.That(root.HasChild("childParallel"), Is.True);

        var childSequence = root.GetChild("childSequence") as ILSProcessLayerNode;
        Assert.That(childSequence, Is.Not.Null);
        Assert.That(childSequence.HasChild("handler1"), Is.True);

        var childSelector = root.GetChild("childSelector") as ILSProcessLayerNode;
        Assert.That(childSelector, Is.Not.Null);
        Assert.That(childSelector.HasChild("handler2"), Is.True);

        var childParallel = root.GetChild("childParallel") as ILSProcessLayerNode;
        Assert.That(childParallel, Is.Not.Null);
        Assert.That(childParallel.HasChild("handler1B"), Is.True);
        Assert.That(childParallel.HasChild("handler2B"), Is.True);

        // Test execution of the entire structure
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, root);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(2)); // handler1 and handler1B
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 and handler2B
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
                .Handler($"handler_{capturedIndex}", (proc, node) => {
                    executionCount++;
                    return LSProcessResultStatus.SUCCESS;
                }));
        }

        var root = builder.Build();
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, root);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = session.Execute();
        stopwatch.Stop();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(executionCount, Is.EqualTo(depth));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000)); // should complete within 1 second
    }

    [Test]
    public void TestComplexHierarchyStateTransitions() {
        var root = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Selector("selector1", sel => sel
                    .Handler("fail1", (proc, node) => LSProcessResultStatus.FAILURE)
                    .Handler("success1", _mockHandler1))
                .Parallel("parallel1", par => par
                    .Handler("handler1", _mockHandler1)
                    .Handler("handler2", _mockHandler2), 2))
            .Build();

        // Test initial state
        Assert.That(root.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(mockProcess, root);

        // Process the entire hierarchy
        var result = session.Execute();

        // Root should succeed
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(root.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }
}
