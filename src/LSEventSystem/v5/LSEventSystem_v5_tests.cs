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
    public void TestBuilderBasicSequence() {
        var context = new LSEventContextBuilder()
            .Sequence("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(context);
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderBasicSelector() {
        var context = new LSEventContextBuilder()
            .Selector("root", (subBuilder) => subBuilder
                .Execute("handler1", _mockHandler1))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(context);
        Assert.That(_handler1CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestBuilderBasicParallel() {
        var context = new LSEventContextBuilder()
            .Parallel("root", subBuilder => subBuilder
                .Execute("handler1", _mockHandler1), 1) // require 1 to succeed
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
        // Test execution
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(context);
        Assert.That(_handler1CallCount, Is.EqualTo(1));

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
                    .Execute("handler2B", _mockHandler2))
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
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(context);
        Assert.That(_handler1CallCount, Is.EqualTo(2)); // handler1 and handler1B
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 and handler2B
        Assert.That(_handler3CallCount, Is.EqualTo(0)); // handler3 should not be called
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
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(newContext);
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // handler1
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // handler2
        Assert.That(_handler3CallCount, Is.EqualTo(0)); // handler3 should not be called
    }

    #endregion

    #region Basic Merge Tests

    [Test]
    public void TestBuilderMergeIntoContext() {
        var rootContext = new LSEventContextBuilder()
            .Sequence("root", seqBuilder => seqBuilder)
            .Build();

        var subContext = new LSEventContextBuilder()
            .Sequence("subContext", seqBuilder => seqBuilder
                .Execute("handler1", _mockHandler1))
            .Build();


        Console.WriteLine($"Sub context: {subContext.NodeID}");
        Console.WriteLine($"Sub context children: {string.Join(", ", Array.ConvertAll(subContext.GetChildren(), c => c.NodeID))}");

        Console.WriteLine($"Root context before merge: {rootContext.NodeID}");
        Console.WriteLine($"Root children before: {string.Join(", ", Array.ConvertAll(rootContext.GetChildren(), c => c.NodeID))}");

        var mergedContext = new LSEventContextBuilder(rootContext)
            .Merge(subContext, merBuilder => merBuilder)
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
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(mergedContext);
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

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root", rootBuilder => rootBuilder
                .Merge(contextA, mABuilder => mABuilder)
                .Merge(contextB, mBBuilder => mBBuilder))
            .Build();

        var root = mergedContext;
        Assert.That(root, Is.Not.Null);
        Assert.That(root.HasChild("sequenceA"), Is.True);
        Assert.That(root.HasChild("sequenceB"), Is.True);

        var seqA = root.GetChild("sequenceA") as ILSEventLayerNode;
        var seqB = root.GetChild("sequenceB") as ILSEventLayerNode;
        Assert.That(seqA?.HasChild("handlerA"), Is.True);
        Assert.That(seqB?.HasChild("handlerB"), Is.True);

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(mergedContext);
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

        var mergedContext = new LSEventContextBuilder()
            .Merge(contextA, mA => mA)
            .Merge(contextB, mB => mB)
            .Build();

        var sequence = mergedContext;
        Assert.That(sequence, Is.Not.Null);

        Assert.That(sequence.HasChild("handler"), Is.True);
        Assert.That(sequence.GetChildren().Length, Is.EqualTo(1));

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

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root", root => root
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        // Navigate to the deep level and verify both handlers exist
        var level1 = mergedContext.GetChild("level1") as ILSEventLayerNode;
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
}
