namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;
/// <summary>
/// Tests for merge operations and context builder functionality.
/// </summary>
[TestFixture]
public class MergeOperationsTests {
    public class MockProcess : LSProcess {
        public MockProcess() { }
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
        //LSProcessManager.DebugLogging();
        LSLogger.Singleton.MinimumLevel = LSLogLevel.DEBUG;
        LSLogger.Singleton.SetSourceStatus(("LSProcessSystem", true));
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
    public void TestBuilderMergeIntoContext() {
        var rootContext = new LSProcessTreeBuilder()
            .Sequence("root", seqBuilder => seqBuilder)
            .Build();


        var subContext = new LSProcessTreeBuilder()
            .Sequence("subContext", seqBuilder => seqBuilder
                .Handler("handler1", _mockHandler1))
            .Build();


        var mergedContext = new LSProcessTreeBuilder(rootContext)
            .Merge(subContext)
            .Build();

        // The merged builder should be the root builder itself, containing the subContext as a child
        Assert.That(mergedContext!.NodeID, Is.EqualTo("root")); // The merged builder is the root
        Assert.That(mergedContext.HasChild("subContext"), Is.True); // The root should contain the subContext
        var subContextNode = mergedContext.GetChild("subContext") as ILSProcessLayerNode;
        Assert.That(subContextNode, Is.Not.Null);
        Assert.That(subContextNode!.HasChild("handler1"), Is.True);

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, mergedContext);
        var result = session.Execute();
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
                .Merge(contextA)
                .Merge(contextB)
            )
            .Build();

        Assert.That(root, Is.Not.Null);
        Assert.That(root.HasChild("sequenceA"), Is.True);
        Assert.That(root.HasChild("sequenceB"), Is.True);

        var seqA = root.GetChild("sequenceA") as ILSProcessLayerNode;
        var seqB = root.GetChild("sequenceB") as ILSProcessLayerNode;
        Assert.That(seqA?.HasChild("handlerA"), Is.True);
        Assert.That(seqB?.HasChild("handlerB"), Is.True);

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, root);
        var result = session.Execute();
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
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        Assert.That(sequence, Is.Not.Null);

        Assert.That(sequence.HasChild("handler"), Is.True);
        Assert.That(sequence.GetChildren().Length, Is.EqualTo(1));

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, sequence);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }

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
        Assert.That(level3!, Is.Not.Null);

        Assert.That(level3!.HasChild("handlerA"), Is.True);
        Assert.That(level3!.HasChild("handlerB"), Is.True);
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
        Assert.That(container, Is.InstanceOf<LSProcessNodeSequence>()); // Should remain a sequence

        var containerLayer = container as ILSProcessLayerNode;
        Assert.That(containerLayer?.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestComplexHierarchyMerge() {
        // Create a complex source builder
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

        // Create a target builder with partial overlap
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
        Assert.That(mainSeq!, Is.Not.Null);

        // Should have validation (new), execution (merged), and cleanup (existing)
        Assert.That(mainSeq!.HasChild("validation"), Is.True);
        Assert.That(mainSeq!.HasChild("execution"), Is.True);
        Assert.That(mainSeq!.HasChild("cleanup"), Is.True);

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

    [Test]
    public void TestMergeNullContext() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root");
        LSProcessBuilderAction subBuilder = null!;

        Assert.Throws<LSArgumentNullException>(() => builder.Merge(subBuilder));

        ILSProcessLayerNode nullNode = null!;
        Assert.Throws<LSArgumentNullException>(() => builder.Merge(nullNode));
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
    public void TestMergeEmptyContext() {
        var emptySequenceRoot = new LSProcessTreeBuilder()
            .Sequence("empty")
            .Build();

        var populatedRoot = new LSProcessTreeBuilder()
            .Sequence("populated", seq => seq
                .Handler("handler1", _mockHandler1))
            .Build();

        var mergedContext = new LSProcessTreeBuilder(populatedRoot)
            .Merge(emptySequenceRoot)
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
        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, mergedContext);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(0)); // First handler should be replaced
        Assert.That(_handler2CallCount, Is.EqualTo(1)); // Second handler should be called
    }
    [Test]
    public void TestMergeWithSubBuilder() {

        var rootBuilder = new LSProcessTreeBuilder()
            .Sequence("rootSequence", rootSeq => rootSeq
                .Merge(subBuilder => subBuilder
                    .Sequence("subSequence", subSeq => subSeq
                        .Handler("subHandler", _mockHandler1))
                ))
            .Build();

        Assert.That(rootBuilder.HasChild("subSequence"), Is.True);
        var subSequence = rootBuilder.GetChild("subSequence") as ILSProcessLayerNode;
        Assert.That(subSequence?.HasChild("subHandler"), Is.True);

        var mockProcess = new MockProcess();
        var session = new LSProcessSession(null!, mockProcess, rootBuilder);
        var result = session.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // subHandler should be called once
    }
}
