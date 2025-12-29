using NUnit.Framework;
using LSUtils.ProcessSystem;
using LSUtils.Logging;
using System;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Test processable for LSProcessManager tests
/// </summary>
internal class TestProcessable : ILSProcessable {
    public Guid ID { get; } = Guid.NewGuid();
    public bool InitializeCalled { get; private set; }

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitialize = null,
        LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
        InitializeCalled = true;
        return LSProcessResultStatus.SUCCESS;
    }
}

[TestFixture]
public class LSProcessManagerTests {
    private LSProcessManager? _manager;

    [SetUp]
    public void SetUp() {
        _manager = new LSProcessManager();
    }

    [TearDown]
    public void TearDown() {
        _manager = null;
    }

    [Test]
    public void Constructor_ShouldInitializeSuccessfully() {
        // Arrange & Act
        var manager = new LSProcessManager();

        // Assert
        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void Singleton_ShouldReturnSameInstance() {
        // Act
        var instance1 = LSProcessManager.Singleton;
        var instance2 = LSProcessManager.Singleton;

        // Assert
        Assert.That(instance1, Is.SameAs(instance2));
        Assert.That(instance1, Is.Not.Null);
    }

    [Test]
    public void DebugLogging_ShouldConfigureLoggingLevels() {
        // Arrange
        var originalLevel = LSLogger.Singleton.MinimumLevel;

        try {
            // Act
            LSProcessManager.DebugLogging(true, LSLogLevel.DEBUG);

            // Assert
            Assert.That(LSLogger.Singleton.MinimumLevel, Is.EqualTo(LSLogLevel.DEBUG));
        } finally {
            // Cleanup
            LSLogger.Singleton.MinimumLevel = originalLevel;
        }
    }

    [Test]
    public void DebugLogging_WithFalse_ShouldDisableLogging() {
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => LSProcessManager.DebugLogging(false));
    }

    [Test]
    public void ProcessInstanceBehaviour_ShouldHaveExpectedValues() {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.ProcessInstanceBehaviour),
            LSProcessManager.ProcessInstanceBehaviour.ALL), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.ProcessInstanceBehaviour),
            LSProcessManager.ProcessInstanceBehaviour.LOCAL), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.ProcessInstanceBehaviour),
            LSProcessManager.ProcessInstanceBehaviour.FIRST), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.ProcessInstanceBehaviour),
            LSProcessManager.ProcessInstanceBehaviour.GLOBAL), Is.True);
    }

    [Test]
    public void GetRootNode_ShouldReturnValidNode() {
        // Arrange
        var process = new TestProcess();
        var processable = new TestProcessable();
        // Use WithProcessing to create the tree using public API
        process.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        var localRoot = process.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(process) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            localRoot,
            out var availableInstances,
            LSProcessManager.ProcessInstanceBehaviour.ALL,
            processable);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithNullLocalRoot_ShouldHandleGracefully() {
        // Arrange
        var processable = new TestProcessable();

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            null,
            out var availableInstances,
            LSProcessManager.ProcessInstanceBehaviour.ALL,
            processable);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithEmptyInstances_ShouldHandleGracefully() {
        // Arrange
        var tempProcess = new TestProcess();
        tempProcess.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        var localRoot = tempProcess.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(tempProcess) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            localRoot,
            out var availableInstances,
            LSProcessManager.ProcessInstanceBehaviour.LOCAL);
    }

    [Test]
    public void GetRootNode_WithNullInstances_ShouldHandleGracefully() {
        // Arrange
        var tempProcess = new TestProcess();
        tempProcess.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        var localRoot = tempProcess.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(tempProcess) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            localRoot,
            out var availableInstances,
            LSProcessManager.ProcessInstanceBehaviour.ALL,
            null);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithFirstBehaviour_ShouldLimitInstances() {
        // Arrange
        var processable1 = new TestProcessable();
        var processable2 = new TestProcessable();
        var tempProcess = new TestProcess();
        tempProcess.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        var localRoot = tempProcess.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(tempProcess) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            localRoot,
            out var availableInstances,
            LSProcessManager.ProcessInstanceBehaviour.FIRST,
            processable1, processable2);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
        // With FIRST behaviour, we expect at most 1 instance
        Assert.That(availableInstances.Length, Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void ProcessWithManager_ShouldExecuteSuccessfully() {
        // Arrange
        var process = new TestProcess();

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsExecuted, Is.True);
    }

    [Test]
    public void MultipleProcesses_ShouldExecuteIndependently() {
        // Arrange
        var process1 = new TestProcess();
        var process2 = new TestProcess();

        // Act
        var result1 = process1.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        var result2 = process2.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process1.IsExecuted, Is.True);
        Assert.That(process2.IsExecuted, Is.True);
        Assert.That(process1.ID, Is.Not.EqualTo(process2.ID));
    }
}
