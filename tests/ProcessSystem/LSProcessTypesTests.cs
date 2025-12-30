using NUnit.Framework;
using LSUtils.ProcessSystem;
using System;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class LSProcessResultStatusTests {
    [Test]
    public void LSProcessResultStatus_ShouldHaveExpectedValues() {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSProcessResultStatus), LSProcessResultStatus.UNKNOWN), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessResultStatus), LSProcessResultStatus.SUCCESS), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessResultStatus), LSProcessResultStatus.FAILURE), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessResultStatus), LSProcessResultStatus.WAITING), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessResultStatus), LSProcessResultStatus.CANCELLED), Is.True);
    }

    [Test]
    public void LSProcessResultStatus_ShouldHaveCorrectValues() {
        // Assert
        Assert.That((int)LSProcessResultStatus.UNKNOWN, Is.EqualTo(0));
        Assert.That((int)LSProcessResultStatus.SUCCESS, Is.EqualTo(1));
        Assert.That((int)LSProcessResultStatus.FAILURE, Is.EqualTo(2));
        Assert.That((int)LSProcessResultStatus.WAITING, Is.EqualTo(3));
        Assert.That((int)LSProcessResultStatus.CANCELLED, Is.EqualTo(4));
    }
}

[TestFixture]
public class LSProcessLayerNodeTypeTests {
    [Test]
    public void LSProcessLayerNodeType_ShouldHaveExpectedValues() {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSProcessLayerNodeType), LSProcessLayerNodeType.SEQUENCE), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessLayerNodeType), LSProcessLayerNodeType.SELECTOR), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessLayerNodeType), LSProcessLayerNodeType.PARALLEL), Is.True);
    }
}

[TestFixture]
public class LSProcessPriorityTests {
    [Test]
    public void LSProcessPriority_ShouldHaveExpectedValues() {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSProcessPriority), LSProcessPriority.LOW), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessPriority), LSProcessPriority.NORMAL), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessPriority), LSProcessPriority.HIGH), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessPriority), LSProcessPriority.CRITICAL), Is.True);
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

        public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitialize = null,
            LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
            InitializeCalled = true;
            LastBuilderAction = onInitialize;
            LastManager = manager;
            LastForwardProcessables = forwardProcessables;
            return LSProcessResultStatus.SUCCESS;
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
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
    
    public LSProcessResultStatus Status { get; set; } = LSProcessResultStatus.UNKNOWN;
    public bool ExecuteCalled { get; private set; }
    public bool CancelCalled { get; private set; }

    public ILSProcessNode Clone() {
        return new TestProcessNode {
            Priority = Priority,
            Conditions = Conditions,
            Order = Order,
            ReadOnly = ReadOnly,
            Status = LSProcessResultStatus.UNKNOWN
        };
    }

    public LSProcessResultStatus Execute(LSProcessSession context) {
        ExecuteCalled = true;
        return Status;
    }

    public LSProcessResultStatus GetNodeStatus() => Status;

    public LSProcessResultStatus Resume(LSProcessSession context, params string[]? nodeIDs) {
        if (Status == LSProcessResultStatus.WAITING) {
            Status = LSProcessResultStatus.SUCCESS;
        }
        return Status;
    }

    public LSProcessResultStatus Fail(LSProcessSession context, params string[]? nodeIDs) {
        Status = LSProcessResultStatus.FAILURE;
        return Status;
    }

    public LSProcessResultStatus Cancel(LSProcessSession context) {
        CancelCalled = true;
        Status = LSProcessResultStatus.CANCELLED;
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
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));

    node.Status = LSProcessResultStatus.SUCCESS;
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
    Assert.That(result, Is.EqualTo(LSProcessResultStatus.UNKNOWN));
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
        return LSProcessResultStatus.SUCCESS;
    }));

    // Act
    var result = process.Execute(LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL);

    // Assert
    Assert.That(node.CancelCalled, Is.True);
    Assert.That(result, Is.EqualTo(LSProcessResultStatus.CANCELLED));
    Assert.That(node.GetNodeStatus(), Is.EqualTo(LSProcessResultStatus.CANCELLED));
}
}
/**/
