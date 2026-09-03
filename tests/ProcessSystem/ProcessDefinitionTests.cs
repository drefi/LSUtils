using System;
using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessDefinitionTests {
    [Test]
    public void Compile_CopiesStructureAndConditions_WithoutExecutingCallbacks() {
        var calls = 0;
        LSProcessNodeCondition condition = _ => { calls++; return true; };
        var conditions = new LSProcessNodeCondition?[] { condition };
        var root = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(root).Selector("choice", s => s
            .Inverter("inverse", i => i.Handler("work", _ => {
                calls++; return LSProcessResultStatus.SUCCESS;
            }, conditions: conditions)));

        var definition = LSProcessDefinition.Compile(root);
        var choice = definition.Root.Children[0];
        var inverse = choice.Children[0];
        var handler = inverse.Children[0];
        conditions[0] = _ => false;
        ((ILSProcessLayerNode)((ILSProcessLayerNode)root.GetChild("choice")!).GetChild("inverse")!)
            .RemoveChild("work");
        root.RemoveChild("choice");

        Assert.That(definition.Root.Children.Count, Is.EqualTo(1));
        Assert.That(choice.Kind, Is.EqualTo(LSProcessDefinitionNodeKind.Selector));
        Assert.That(inverse.Kind, Is.EqualTo(LSProcessDefinitionNodeKind.Inverter));
        Assert.That(handler.Kind, Is.EqualTo(LSProcessDefinitionNodeKind.Handler));
        Assert.That(handler.Conditions[0], Is.SameAs(condition));
        Assert.That(handler.Handler, Is.Not.Null);
        Assert.That(calls, Is.Zero);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<LSProcessNodeDefinition>)definition.Root.Children).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<LSProcessNodeCondition?>)handler.Conditions)[0] = null);
    }

    [Test]
    public void Compile_PreservesOrderPriorityAndPolicies() {
        var root = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(root)
            .Handler("first", _ => LSProcessResultStatus.SUCCESS,
                NodeUpdatePolicy.READONLY, LSProcessPriority.LOW)
            .Handler("second", _ => LSProcessResultStatus.SUCCESS,
                priority: LSProcessPriority.HIGH);
        var definition = LSProcessDefinition.Compile(root);
        var source = root.GetChild("first")!;

        Assert.That(definition.Root.Kind, Is.EqualTo(LSProcessDefinitionNodeKind.Sequence));
        Assert.That(definition.Root.Children[0].NodeID, Is.EqualTo("first"));
        Assert.That(definition.Root.Children[0].Order, Is.EqualTo(source.Order));
        Assert.That(definition.Root.Children[0].Priority, Is.EqualTo(source.Priority));
        Assert.That(definition.Root.Children[0].UpdatePolicy, Is.EqualTo(source.UpdatePolicy));
        Assert.That(definition.Root.Children[1].NodeID, Is.EqualTo("second"));
    }

    [Test]
    public void LaterManagerRegistration_DoesNotAlterCompiledDefinition() {
        var manager = new LSProcessManager();
        manager.Register<PipelineTestProcess>(b => b.Handler("first", _ => LSProcessResultStatus.SUCCESS));
        var first = LSProcessDefinition.Compile(manager.GetRootNode(typeof(PipelineTestProcess), out _));
        manager.Register<PipelineTestProcess>(b => b.Handler("second", _ => LSProcessResultStatus.SUCCESS));
        var second = LSProcessDefinition.Compile(manager.GetRootNode(typeof(PipelineTestProcess), out _));

        Assert.That(first.Root.Children.Count, Is.EqualTo(1));
        Assert.That(second.Root.Children.Count, Is.EqualTo(2));
    }

    [Test]
    public void Compile_RejectsCyclesWithoutChangingSource() {
        var root = LSProcessManager.CreateRootNode("root");
        root.AddChild(root);
        Assert.Throws<InvalidOperationException>(() => LSProcessDefinition.Compile(root));
        Assert.That(root.GetChild("root"), Is.SameAs(root));
    }
}
