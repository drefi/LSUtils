using NUnit.Framework;
using LSUtils.ProcessSystem;
using System;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class LSProcessResultTests {
    [Test]
    public void OutcomesHaveExclusiveSemanticFlags() {
        Assert.Multiple(() => {
            Assert.That(LSProcessResult.NotExecuted.IsNotExecuted, Is.True);
            Assert.That(LSProcessResult.Success.IsSuccess, Is.True);
            Assert.That(LSProcessResult.Failure.IsFailure, Is.True);
            Assert.That(LSProcessResult.Waiting.IsWaiting, Is.True);
            Assert.That(LSProcessResult.Cancelled.IsCancelled, Is.True);
            Assert.That(LSProcessResult.Undetermined.IsUndetermined, Is.True);
            Assert.That(LSProcessResult.NotExecuted, Is.Not.EqualTo(LSProcessResult.Undetermined));
            Assert.That(LSProcessResult.Success.IsTerminal, Is.True);
            Assert.That(LSProcessResult.Waiting.IsTerminal, Is.False);
        });
    }

    [Test]
    public void TypedPayloadCanBeReadOnlyAsCompatibleType() {
        var reason = new InvalidOperationException("denied");
        var result = LSProcessResult.Failed(reason);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.HasPayload, Is.True);
        Assert.That(result.PayloadType, Is.EqualTo(typeof(InvalidOperationException)));
        Assert.That(result.GetPayload<Exception>(), Is.SameAs(reason));
        Assert.That(result.TryGetPayload<string>(out _), Is.False);
        Assert.Throws<InvalidOperationException>(() => result.GetPayload<string>());
    }

    [Test]
    public void FlowEqualityIgnoresDiagnosticPayload() {
        Assert.That(LSProcessResult.Failed("first"), Is.EqualTo(LSProcessResult.Failed(42)));
        Assert.That(LSProcessResult.Failed("first"), Is.EqualTo(LSProcessResult.Failure));
        Assert.That(LSProcessResult.Failed("first"), Is.Not.EqualTo(LSProcessResult.Success));
    }
}

[TestFixture]
public class LSProcessPriorityTests {
    [Test]
    public void LSProcessPriority_ShouldHaveExpectedValues() {
        Assert.That(LSProcessPriority.MINIMAL.Value, Is.EqualTo(0));
        Assert.That(LSProcessPriority.LOW.CompareTo(LSProcessPriority.NORMAL), Is.LessThan(0));
        Assert.That(LSProcessPriority.HIGH.CompareTo(LSProcessPriority.NORMAL), Is.GreaterThan(0));
        Assert.That(LSProcessPriority.CRITICAL.ToString(), Is.EqualTo("CRITICAL"));
    }
}

[TestFixture]
public class ILSProcessableTests {
    private class TestProcessable : ILSProcessable {
        public Guid ID { get; } = Guid.NewGuid();
        public bool InitializeCalled { get; private set; }
        public LSProcessBuilderAction? LastBuilderAction { get; private set; }
        public LSProcessManager? LastManager { get; private set; }
        public ILSProcessable[]? LastForwardProcessables { get; private set; }

        public LSProcessResult Initialize(LSProcessBuilderAction? onInitialize = null,
            LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
            InitializeCalled = true;
            LastBuilderAction = onInitialize;
            LastManager = manager;
            LastForwardProcessables = forwardProcessables;
            return LSProcessResult.Success;
        }
    }

    [Test]
    public void TestProcessable_ShouldImplementInterface() {
        // Arrange & Act
        var processable = new TestProcessable();

        // Assert
        Assert.That(processable, Is.InstanceOf<ILSProcessable>());
        Assert.That(processable.ID, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void Initialize_ShouldBeCallable() {
        // Arrange
        var processable = new TestProcessable();

        // Act
        var result = processable.Initialize();

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResult.Success));
        Assert.That(processable.InitializeCalled, Is.True);
    }

    [Test]
    public void Initialize_WithParameters_ShouldStoreParameters() {
        // Arrange
        var processable = new TestProcessable();
        var manager = new LSProcessManager();
        var forwardProcessable = new TestProcessable();
        LSProcessBuilderAction builderAction = builder => builder;

        // Act
        var result = processable.Initialize(builderAction, manager, forwardProcessable);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResult.Success));
        Assert.That(processable.LastBuilderAction, Is.EqualTo(builderAction));
        Assert.That(processable.LastManager, Is.EqualTo(manager));
        Assert.That(processable.LastForwardProcessables, Is.Not.Null);
        Assert.That(processable.LastForwardProcessables!.Length, Is.EqualTo(1));
        Assert.That(processable.LastForwardProcessables[0], Is.EqualTo(forwardProcessable));
    }

    [Test]
    public void Initialize_WithNullParameters_ShouldHandleGracefully() {
        // Arrange
        var processable = new TestProcessable();

        // Act
        var result = processable.Initialize(null, null, null);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResult.Success));
        Assert.That(processable.LastBuilderAction, Is.Null);
        Assert.That(processable.LastManager, Is.Null);
        Assert.That(processable.LastForwardProcessables, Is.Null);
    }
}

/** It does not make sense ILSProcessNode have tests because LSProcess/LSProcessManager will not implement then, so they cannot be testes by the process.
// I will leave this for demonstration purposes only, but this is unnecessary.
[TestFixture]
public class ILSProcessNodeTests {
private class TestProcessNode : ILSProcessNode {
    public string NodeID { get; } = Guid.NewGuid().ToString();
    public LSProcessPriority Priority { get; set; } = LSProcessPriority.NORMAL;
    public LSProcessNodeCondition? Conditions { get; set; }
    public int ExecutionCount { get; private set; }
    public int Order { get; set; }
    public bool ReadOnly { get; set; }
    
    public LSProcessResult Status { get; set; } = LSProcessResult.Undetermined;
    public bool ExecuteCalled { get; private set; }
    public bool CancelCalled { get; private set; }

    public ILSProcessNode Clone() {
        return new TestProcessNode {
            Priority = Priority,
            Conditions = Conditions,
            Order = Order,
            ReadOnly = ReadOnly,
            Status = LSProcessResult.Undetermined
        };
    }

    public LSProcessResult Execute(LSProcessSession context) {
        ExecuteCalled = true;
        return Status;
    }

    public LSProcessResult GetNodeStatus() => Status;

    public LSProcessResult Resume(LSProcessSession context) {
        if (Status == LSProcessResult.Waiting) {
            Status = LSProcessResult.Success;
        }
        return Status;
    }

    public LSProcessResult Fail(LSProcessSession context) {
        Status = LSProcessResult.Failure;
        return Status;
    }

    public LSProcessResult Cancel(LSProcessSession context) {
        CancelCalled = true;
        Status = LSProcessResult.Cancelled;
        return Status;
    }
}

[Test]
public void TestProcessNode_ShouldImplementInterface() {
    // Arrange & Act
    var node = new TestProcessNode();

    // Assert
    Assert.That(node, Is.InstanceOf<ILSProcessNode>());
    Assert.That(node.NodeID, Is.Not.Null);
    Assert.That(node.NodeID, Is.Not.Empty);
}

[Test]
public void GetNodeStatus_ShouldReturnCurrentStatus() {
    // Arrange
    var node = new TestProcessNode();

    // Act & Assert
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResult.Undetermined));

    node.Status = LSProcessResult.Success;
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResult.Success));
}

[Test]
public void Execute_ShouldBeCallable() {
    // Arrange
    var node = new TestProcessNode();
    var process = new TestProcess();
    // Use public API to create session through process execution
    process.WithProcessing(builder => builder.Handler("test", session => {
        // This will trigger node execution through the proper API
        return node.Execute(session);
    }));

    // Act
    var result = process.Execute(LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL);

    // Assert
    Assert.That(node.ExecuteCalled, Is.True);
    Assert.That(result, Is.EqualTo(LSProcessResult.Undetermined));
}

[Test]
public void Cancel_ShouldSetCancelledStatus() {
    // Arrange
    var node = new TestProcessNode();
    var process = new TestProcess();
    // Use public API to create session through process execution
    process.WithProcessing(builder => builder.Handler("test", session => {
        // This will test the cancel functionality
        var result = node.Cancel(session);
        return LSProcessResult.Success;
    }));

    // Act
    var result = process.Execute(LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL);

    // Assert
    Assert.That(node.CancelCalled, Is.True);
    Assert.That(result, Is.EqualTo(LSProcessResult.Cancelled));
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResult.Cancelled));
}
}
/**/
