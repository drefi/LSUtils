using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem.TestV5;

/// <summary>
/// Comprehensive NUnit tests for LSEventSystem v5, focusing on LSEventContextBuilder.
/// </summary>
[TestFixture]
public class LSEventContextBuilderTests {
    
    private LSEventHandler _mockHandler1;
    private LSEventHandler _mockHandler2;
    private LSEventHandler _mockHandler3;
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
        
        _mockHandler3 = (evt, node) => {
            _handler3CallCount++;
            return LSEventProcessStatus.SUCCESS;
        };
    }

    [TearDown]
    public void Cleanup() {
        // Reset for next test
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;
    }

    #region Basic Builder Tests

    [Test]
    public void TestBasicSequenceBuilding() {
        var context = new LSEventContextBuilder()
            .Sequence("root")
                .Handler("handler1", _mockHandler1)
            .End()
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
    }

    [Test]
    public void TestBasicSelectorBuilding() {
        var context = new LSEventContextBuilder()
            .Selector("root")
                .Handler("handler1", _mockHandler1)
            .End()
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("handler1"), Is.True);
    }

    [Test]
    public void TestNestedStructureBuilding() {
        var context = new LSEventContextBuilder()
            .Sequence("root")
                .Sequence("childSequence")
                    .Handler("handler1", _mockHandler1)
                .End()
                .Selector("childSelector")
                    .Handler("handler2", _mockHandler2)
                .End()
            .End()
            .Build();

        Assert.That(context.NodeID, Is.EqualTo("root"));
        Assert.That(context.HasChild("childSequence"), Is.True);
        Assert.That(context.HasChild("childSelector"), Is.True);
        
        var childSequence = context.FindChild("childSequence") as ILSEventLayerNode;
        Assert.That(childSequence, Is.Not.Null);
        Assert.That(childSequence.HasChild("handler1"), Is.True);
        
        var childSelector = context.FindChild("childSelector") as ILSEventLayerNode;
        Assert.That(childSelector, Is.Not.Null);
        Assert.That(childSelector.HasChild("handler2"), Is.True);
    }

    #endregion

    #region Basic Merge Tests

    [Test]
    public void TestMergeIntoEmptyContext() {
        var sourceContext = new LSEventContextBuilder()
            .Sequence("source")
                .Handler("handler1", _mockHandler1)
            .End()
            .Build();

        var targetContext = new LSEventContextBuilder()
            .Sequence("target")
            .End()
            .Build();

        Console.WriteLine($"Source context: {sourceContext.NodeID}");
        Console.WriteLine($"Source children: {string.Join(", ", Array.ConvertAll(sourceContext.GetChildren(), c => c.NodeID))}");
        
        Console.WriteLine($"Target context before merge: {targetContext.NodeID}");
        Console.WriteLine($"Target children before: {string.Join(", ", Array.ConvertAll(targetContext.GetChildren(), c => c.NodeID))}");

        var mergedContext = new LSEventContextBuilder(targetContext)
            .Merge(sourceContext) //this will merge source into target: Sequence["target"] -> Sequence["source"] -> Handler["handler1"]
            //target should be seen as the root node
            .Build();

        Console.WriteLine($"Merged context: {mergedContext.NodeID}");
        Console.WriteLine($"Merged children: {string.Join(", ", Array.ConvertAll(mergedContext.GetChildren(), c => c.NodeID))}");

        // The merged context should be the target context itself, containing the source as a child
        Assert.That(mergedContext.NodeID, Is.EqualTo("target")); // The merged context is the target
        Assert.That(mergedContext.HasChild("source"), Is.True); // The target should contain the source
        var sourceNode = mergedContext.FindChild("source") as ILSEventLayerNode;
        Assert.That(sourceNode, Is.Not.Null);
        Assert.That(sourceNode.HasChild("handler1"), Is.True);
    }

    [Test]
    public void TestMergeNonConflictingNodes() {
        var contextA = new LSEventContextBuilder()
            .Sequence("sequenceA")
                .Handler("handlerA", _mockHandler1)
            .End()
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("sequenceB")
                .Handler("handlerB", _mockHandler2)
            .End()
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root")
            .End()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        Assert.That(mergedContext.HasChild("sequenceA"), Is.True);
        Assert.That(mergedContext.HasChild("sequenceB"), Is.True);
        
        var seqA = mergedContext.FindChild("sequenceA") as ILSEventLayerNode;
        var seqB = mergedContext.FindChild("sequenceB") as ILSEventLayerNode;
        
        Assert.That(seqA?.HasChild("handlerA"), Is.True);
        Assert.That(seqB?.HasChild("handlerB"), Is.True);
    }

    [Test]
    public void TestMergeHandlerReplacement() {
        var contextA = new LSEventContextBuilder()
            .Sequence("sequence")
                .Handler("handler", _mockHandler1)
            .End()
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("sequence")
                .Handler("handler", _mockHandler2)
            .End()
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root")
            .End()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        Assert.That(mergedContext.HasChild("sequence"), Is.True);
        var sequence = mergedContext.FindChild("sequence") as ILSEventLayerNode;
        Assert.That(sequence?.HasChild("handler"), Is.True);
        
        // The handler should be replaced with handler2
        // We can't directly test which handler it is, but we can verify there's only one
        Assert.That(sequence?.GetChildren().Length, Is.EqualTo(1));
    }

    #endregion

    #region Recursive Merge Tests

    [Test]
    public void TestDeepRecursiveMerge() {
        var contextA = new LSEventContextBuilder()
            .Sequence("level1")
                .Sequence("level2")
                    .Sequence("level3")
                        .Handler("handlerA", _mockHandler1)
                    .End()
                .End()
            .End()
            .Build();

        var contextB = new LSEventContextBuilder()
            .Sequence("level1")
                .Sequence("level2")
                    .Sequence("level3")
                        .Handler("handlerB", _mockHandler2)
                    .End()
                .End()
            .End()
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root")
            .End()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        // Navigate to the deep level and verify both handlers exist
        var level1 = mergedContext.FindChild("level1") as ILSEventLayerNode;
        var level2 = level1?.FindChild("level2") as ILSEventLayerNode;
        var level3 = level2?.FindChild("level3") as ILSEventLayerNode;

        Assert.That(level3, Is.Not.Null);
        Assert.That(level3.HasChild("handlerA"), Is.True);
        Assert.That(level3.HasChild("handlerB"), Is.True);
    }

    [Test]
    public void TestMixedNodeTypeMerge() {
        var contextA = new LSEventContextBuilder()
            .Sequence("container")
                .Handler("handler1", _mockHandler1)
            .End()
            .Build();

        var contextB = new LSEventContextBuilder()
            .Selector("container")
                .Handler("handler2", _mockHandler2)
            .End()
            .Build();

        var mergedContext = new LSEventContextBuilder()
            .Sequence("root")
            .End()
            .Merge(contextA)
            .Merge(contextB)
            .Build();

        // The container should be replaced with the selector from contextB
        Assert.That(mergedContext.HasChild("container"), Is.True);
        var container = mergedContext.FindChild("container");
        Assert.That(container, Is.InstanceOf<LSEventSelectorNode>());
        
        var containerLayer = container as ILSEventLayerNode;
        Assert.That(containerLayer?.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestComplexHierarchyMerge() {
        // Create a complex source context
        var sourceContext = new LSEventContextBuilder()
            .Sequence("mainSequence")
                .Selector("validation")
                    .Handler("validateUser", _mockHandler1)
                    .Handler("validatePermissions", _mockHandler2)
                .End()
                .Sequence("execution")
                    .Handler("executeAction", _mockHandler3)
                .End()
            .End()
            .Build();

        // Create a target context with partial overlap
        var targetContext = new LSEventContextBuilder()
            .Sequence("root")
                .Sequence("mainSequence")
                    .Sequence("execution")
                        .Handler("preExecute", _mockHandler1)
                    .End()
                    .Handler("cleanup", _mockHandler2)
                .End()
            .End()
            .Build();

        var mergedContext = new LSEventContextBuilder(targetContext)
            .Navigate("mainSequence")
                .Merge((sourceContext.FindChild("mainSequence") as ILSEventLayerNode)!)
            .End()
            .Build();

        // Verify the structure
        var mainSeq = mergedContext.FindChild("mainSequence") as ILSEventLayerNode;
        Assert.That(mainSeq, Is.Not.Null);
        
        // Should have validation (new), execution (merged), and cleanup (existing)
        Assert.That(mainSeq.HasChild("validation"), Is.True);
        Assert.That(mainSeq.HasChild("execution"), Is.True);
        Assert.That(mainSeq.HasChild("cleanup"), Is.True);
        
        // Check validation node
        var validation = mainSeq.FindChild("validation") as ILSEventLayerNode;
        Assert.That(validation, Is.InstanceOf<LSEventSelectorNode>());
        Assert.That(validation?.HasChild("validateUser"), Is.True);
        Assert.That(validation?.HasChild("validatePermissions"), Is.True);
        
        // Check execution node - should have both handlers
        var execution = mainSeq.FindChild("execution") as ILSEventLayerNode;
        Assert.That(execution?.HasChild("preExecute"), Is.True);
        Assert.That(execution?.HasChild("executeAction"), Is.True);
    }

    #endregion

    #region Error Condition Tests

    [Test]
    public void TestMergeNullContext() {
        var builder = new LSEventContextBuilder()
            .Sequence("root")
            .End();

        Assert.Throws<LSArgumentNullException>(() => builder.Merge(null!));
    }

    [Test]
    public void TestMergeWithoutCurrentNode() {
        var sourceContext = new LSEventContextBuilder()
            .Sequence("source")
            .End()
            .Build();

        var builder = new LSEventContextBuilder();
        
        Assert.Throws<LSException>(() => builder.Merge(sourceContext));
    }

    [Test]
    public void TestNavigateToNonExistentNode() {
        var context = new LSEventContextBuilder()
            .Sequence("root")
                .Handler("handler1", _mockHandler1)
            .End()
            .Build();

        var builder = new LSEventContextBuilder(context);
        
        Assert.Throws<LSException>(() => builder.Navigate("nonExistent"));
    }

    [Test]
    public void TestRemoveNonExistentNode() {
        var builder = new LSEventContextBuilder()
            .Sequence("root")
            .End();

        // Should not throw, just return false or handle gracefully
        Assert.DoesNotThrow(() => builder.Remove("nonExistent"));
    }

    #endregion

    #region Navigation Tests

    [Test]
    public void TestNavigateAndModify() {
        var context = new LSEventContextBuilder()
            .Sequence("root")
                .Sequence("child")
                    .Handler("handler1", _mockHandler1)
                .End()
            .End()
            .Build();

        var modifiedContext = new LSEventContextBuilder(context)
            .Navigate("child")
                .Handler("handler2", _mockHandler2)
                .Remove("handler1")
            .End()
            .Build();

        var child = modifiedContext.FindChild("child") as ILSEventLayerNode;
        Assert.That(child, Is.Not.Null);
        Assert.That(child.HasChild("handler2"), Is.True);
        Assert.That(child.HasChild("handler1"), Is.False);
    }

    #endregion
}

