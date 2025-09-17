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

        var sequence = mergedContext;
        Assert.That(sequence.HasChild("handler"), Is.True);

        // Since override is always allowed, the second handler should replace the first
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(mergedContext);
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
    public void TestHandlerWithConditions() {
        bool conditionMet = false;
        LSEventCondition condition = (evt, node) => conditionMet;

        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("conditionalHandler", (ctx, node) => {
                    _handler1CallCount++;
                    return LSEventProcessStatus.FAILURE;
                }, LSPriority.NORMAL, condition))
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);

        var result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // since we skipped the handler that would return FAILURE the result is SUCCESS
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // handler should not be called because conditionMet == false

        // Condition met - handler should run
        conditionMet = true;
        var newProcessContext = new LSEventProcessContext(mockEvent); // process context cannot be reused
        result = newProcessContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // since we now run the handler that returns FAILURE as the handler returns FAILURE
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Handler should be called
    }

    [Test]
    public void TestParallelPartialResult() {
        var context = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("successHandler", _mockHandler1)
                .Execute("failureHandler", _mockHandler3Failure)
                .Execute("anotherFailure", _mockHandler3Failure), 1)
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // since _mockHandler1 should succeed the result must be SUCCESS because the parallel requires only 1 success
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); // handlers are called, the result that has failed

        // success threshold higher than available successes
        var newContext = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("successHandler", _mockHandler1)
                .Execute("failureHandler", _mockHandler3Failure)
                .Execute("anotherFailure", _mockHandler3Failure), 2)
            .Build();
        var newProcessContext = new LSEventProcessContext(mockEvent); // process context cannot be reused
        result = newProcessContext.Process(newContext);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // since we need 2 successes and only 1 is available
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(2)); // handlers are called, the result that has failed
    }

    [Test]
    public void TestSelectorShortCircuit() {
        var context = new LSEventContextBuilder()
            .Selector("root", sel => sel
                .Execute("firstHandler", _mockHandler1) // Success
                .Execute("secondHandler", _mockHandler2)) // Should not be called
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(context);

        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0));
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
    public void TestExecutionOrderAfterMerge() {
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
        var processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(mergedContext);

        // in the same priority order should be preserved
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "A", "B" }));

        // Now test with different priorities
        executionOrder.Clear();
        
        // Create fresh contexts for the second test
        var contextA2 = new LSEventContextBuilder()
            .Sequence("seqA", seq => seq
                .Execute("handlerA", (evt, node) => {
                    executionOrder.Add("A");
                    return LSEventProcessStatus.SUCCESS;
                }))
            .Build();

        var contextB2 = new LSEventContextBuilder()
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
                .Merge(contextA2)
                .Merge(contextC) // C has higher priority
                .Merge(contextB2))
            .Build();

        mockEvent = new MockEvent(_mockDispatcher);
        processContext = new LSEventProcessContext(mockEvent);
        processContext.Process(mergedContextWithPriority);

        Assert.That(executionOrder, Is.EqualTo(new List<string> { "C", "A", "B" }));
    }

    #endregion

    #region LSEventProcessContext Tests

    [Test]
    public void TestProcessContextSuccess() {
        //sequence success
        var context = new LSEventContextBuilder()
            .Sequence("root")
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        //selector success
        context = new LSEventContextBuilder()
            .Selector("root")
            .Build();
        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        //parallel success
        context = new LSEventContextBuilder()
            .Parallel("root")
            .Build();

        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
    }

    [Test]
    public void TestProcessContextFailure() {
        // sequence failure
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("failureHandler", _mockHandler3Failure))
            .Build();
        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));

        // selector failure
        context = new LSEventContextBuilder()
            .Selector("root", sel => sel
                .Execute("failureHandler", _mockHandler3Failure))
            .Build();
        processContext = new LSEventProcessContext(mockEvent);
        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));

        // parallel failure
        context = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("failureHandler", _mockHandler3Failure))
            .Build();
        processContext = new LSEventProcessContext(mockEvent);
        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE));
    }

    [Test]
    public void TestProcessContextCancel() {
        // sequence cancel
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("cancelHandler", _mockHandler3Cancel))
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.CANCELLED));

        // selector cancel
        context = new LSEventContextBuilder()
            .Selector("root", sel => sel
                .Execute("cancelHandler", _mockHandler3Cancel))
            .Build();
        processContext = new LSEventProcessContext(mockEvent);
        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.CANCELLED));

        // parallel cancel
        context = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("cancelHandler", _mockHandler3Cancel))
            .Build();
        processContext = new LSEventProcessContext(mockEvent);
        result = processContext.Process(context);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.CANCELLED));
    }

    [Test]
    public void TestProcessSequenceWaitingAndResume() {
        int handlerExecuted = 0;
        LSEventHandlerNode? waitingNode = null;
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("waitingHandler", (evt, node) => {
                    waitingNode = node as LSEventHandlerNode;
                    handlerExecuted++;
                    return LSEventProcessStatus.WAITING;
                })
                .Execute("waitingHandler", (evt, node) => {
                    handlerExecuted++;
                    return LSEventProcessStatus.WAITING;
                }))
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(handlerExecuted, Is.EqualTo(1)); // Only first handler should execute
        Assert.That(waitingNode, Is.Not.Null);
        result = processContext.Resume(waitingNode);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // Should succeed after resume
        Assert.That(handlerExecuted, Is.EqualTo(2)); // Second handler should execute after resume
    }
    [Test]
    public void TestProcessSelectorWaitingAndResume() {
        LSEventHandlerNode? waitingNode = null;
        var context = new LSEventContextBuilder()
            .Selector("root", sel => sel
                .Execute("failureHandler", _mockHandler3Failure)
                .Execute("waitingHandler", (evt, node) => {
                    waitingNode = node as LSEventHandlerNode;
                    _handler1CallCount++;
                    return LSEventProcessStatus.WAITING;
                })
                .Execute("shouldNotCallHandler", _mockHandler2)) // Should not be called because we are Resuming from waiting
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); // Failure called
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Waiting handler called
        Assert.That(waitingNode, Is.Not.Null);

        result = processContext.Resume(waitingNode);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // Should succeed after resume
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Should not call shouldNotCallHandler after resume
    }
    [Test]
    public void TestProcessParallelWaitingAndResume() {
        LSEventHandlerNode? waitingNode1 = null;
        LSEventHandlerNode? waitingNode2 = null;

        var context = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("waiting1", (evt, node) => {
                    waitingNode1 = node as LSEventHandlerNode;
                    _handler1CallCount++;
                    return LSEventProcessStatus.WAITING;
                })
                .Execute("waiting2", (evt, node) => {
                    waitingNode2 = node as LSEventHandlerNode;
                    _handler2CallCount++;
                    return LSEventProcessStatus.WAITING;
                }), 2)
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
        Assert.That(waitingNode1, Is.Not.Null);
        Assert.That(waitingNode2, Is.Not.Null);

        // Resume first node
        result = processContext.Resume(waitingNode1);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING)); // Still waiting for the second

        // Resume second node
        result = processContext.Resume(waitingNode2);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS)); // Should succeed after both resumed
    }

    [Test]
    public void TestProcessContextSequenceWaitingAndFail() {
        LSEventHandlerNode? waitingNode = null;
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("handler1", _mockHandler1)
                .Execute("waitingHandler", (evt, node) => {
                    waitingNode = node as LSEventHandlerNode;
                    _handler3CallCount++;
                    return LSEventProcessStatus.WAITING;
                }))
                .Execute("handler2", _mockHandler2)
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING)); // Currently succeeds instead of waiting
        Assert.That(_handler3CallCount, Is.EqualTo(1)); // Waiting handler called
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // First handler called
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Second handler not called
        Assert.That(waitingNode, Is.Not.Null);
        result = processContext.Fail(waitingNode);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // Should succeed after
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Second handler should not be called after Fail()
    }

    [Test]
    public void TestProcessContextSelectorWaitingAndFail() {
        LSEventHandlerNode? waitingNode = null;

        var context = new LSEventContextBuilder()
            .Selector("root", sel => sel
                .Execute("failureHandler", _mockHandler3Failure)
                .Execute("waitingHandler", (evt, node) => {
                    waitingNode = node as LSEventHandlerNode;
                    _handler1CallCount++;
                    return LSEventProcessStatus.WAITING;
                })
                .Execute("shouldCallHandler", _mockHandler2)) // Should be called because we are failing from waiting
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING)); // Currently succeeds instead of waiting
        Assert.That(_handler3CallCount, Is.EqualTo(1)); // _mockHandler3Failure called
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Waiting handler called
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Second handler not called
        Assert.That(waitingNode, Is.Not.Null);

        result = processContext.Fail(waitingNode);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // Should result in fail
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Should call shouldCallHandler after Fail()
    }
    //parallel waiting and fail
    [Test]
    public void TestProcessContextParallelWaitingAndFail() {
        LSEventHandlerNode? waitingNode1 = null;
        LSEventHandlerNode? waitingNode2 = null;

        var context = new LSEventContextBuilder()
            .Parallel("root", par => par
                .Execute("waiting1", (evt, node) => {
                    waitingNode1 = node as LSEventHandlerNode;
                    _handler1CallCount++;
                    return LSEventProcessStatus.WAITING;
                })
                .Execute("waiting2", (evt, node) => {
                    waitingNode2 = node as LSEventHandlerNode;
                    _handler2CallCount++;
                    return LSEventProcessStatus.WAITING;
                }), 2)
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
        Assert.That(waitingNode1, Is.Not.Null);
        Assert.That(waitingNode2, Is.Not.Null);

        // Fail first node
        result = processContext.Fail(waitingNode1);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING)); // Still waiting for the second

        // Fail second node
        result = processContext.Fail(waitingNode2);
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.FAILURE)); // Should fail after both failed
    }

    [Test]
    public void TestProcessContextSequenceWaitingAndCancel() {
        LSEventHandlerNode? waitingNode = null;
        var context = new LSEventContextBuilder()
            .Sequence("root", seq => seq
                .Execute("cancelHandler", (evt, node) => {
                    _handler3CallCount++;
                    waitingNode = node as LSEventHandlerNode;
                    return LSEventProcessStatus.WAITING;
                }))
                .Execute("handler1", _mockHandler1)
            .Build();

        var mockEvent = new MockEvent(_mockDispatcher);
        var processContext = new LSEventProcessContext(mockEvent);
        var result = processContext.Process(context);

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.WAITING));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); // Waiting handler called
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // Second handler not called

        processContext.Cancel(); // Cancel the entire process

        Assert.That(processContext.IsCancelled, Is.True);
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // Second handler not called

        Assert.That(waitingNode, Is.Not.Null);
        result = processContext.Resume(waitingNode); // Resuming after cancel should have no effect
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.CANCELLED)); // Should remain cancelled
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // Second handler should not be called after Resume when already cancelled
    }


    #endregion
}
