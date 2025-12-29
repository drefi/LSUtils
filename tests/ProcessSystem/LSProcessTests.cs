using NUnit.Framework;
using LSUtils.ProcessSystem;
using LSUtils.Logging;
using System;
using System.Collections.Generic;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Concrete implementation of LSProcess for testing purposes
/// </summary>
internal class TestProcess : LSProcess {
    public string TestData { get; set; } = "";
    public bool ProcessingCalled { get; private set; }

    public TestProcess() : base() { }

    public TestProcess(Dictionary<string, object> data) : base(data) { }

    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        ProcessingCalled = true;
        return builder.Handler("test-handler", session => {
            TestData = "processed";
            return LSProcessResultStatus.SUCCESS;
        });
    }
}

/// <summary>
/// Test process that simulates failure
/// </summary>
internal class FailingTestProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Handler("failing-handler", session => {
            return LSProcessResultStatus.FAILURE;
        });
    }
}

/// <summary>
/// Test process that simulates waiting state
/// </summary>
internal class WaitingTestProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Handler("waiting-handler", session => {
            return LSProcessResultStatus.WAITING;
        });
    }
}

[TestFixture]
public class LSProcessTests {
    private LSProcessManager? _manager;
    private TestProcess? _process;

    [SetUp]
    public void SetUp() {
        _manager = new LSProcessManager();
        _process = new TestProcess();
    }

    [TearDown]
    public void TearDown() {
        _manager = null;
        _process = null;
    }

    [Test]
    public void Constructor_ShouldInitializeWithUniqueId() {
        // Arrange & Act
        var process1 = new TestProcess();
        var process2 = new TestProcess();

        // Assert
        Assert.That(process1.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(process2.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(process1.ID, Is.Not.EqualTo(process2.ID));
    }

    [Test]
    public void Constructor_ShouldSetCreatedAt() {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var process = new TestProcess();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.That(process.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreation));
        Assert.That(process.CreatedAt, Is.LessThanOrEqualTo(afterCreation));
    }

    [Test]
    public void Constructor_WithData_ShouldInitializeData() {
        // Arrange
        var data = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        // Act
        var process = new TestProcess(data);

        // Assert
        Assert.That(process.TryGetData<string>("key1", out var value1), Is.True);
        Assert.That(value1, Is.EqualTo("value1"));
        Assert.That(process.TryGetData<int>("key2", out var value2), Is.True);
        Assert.That(value2, Is.EqualTo(42));
    }

    [Test]
    public void IsExecuted_ShouldReturnFalseInitially() {
        // Assert
        Assert.That(_process!.IsExecuted, Is.False);
    }

    [Test]
    public void IsCancelled_ShouldReturnFalseInitially() {
        // Assert
        Assert.That(_process!.IsCancelled, Is.False);
    }

    [Test]
    public void IsCompleted_ShouldReturnFalseInitially() {
        // Assert
        Assert.That(_process!.IsCompleted, Is.False);
    }

    [Test]
    public void Execute_WithManager_ShouldReturnSuccess() {
        // Act
        var result = _process!.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_process.IsExecuted, Is.True);
        Assert.That(_process.IsCompleted, Is.True);
        Assert.That(_process.TestData, Is.EqualTo("processed"));
        Assert.That(_process.ProcessingCalled, Is.True);
    }

    [Test]
    public void Execute_WithoutManager_ShouldUsesSingleton() {
        // Act
        var result = _process!.Execute();

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_process.IsExecuted, Is.True);
        Assert.That(_process.IsCompleted, Is.True);
    }

    [Test]
    public void Execute_WithNullManager_ShouldThrowException() {
        // Act & Assert
        Assert.Throws<LSException>(() =>
            _process!.Execute(null!, LSProcessManager.ProcessInstanceBehaviour.ALL));
    }

    [Test]
    public void Execute_CalledTwice_ShouldReturnCachedResult() {
        // Arrange
        var firstResult = _process!.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Act
        var secondResult = _process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(firstResult, Is.EqualTo(secondResult));
        Assert.That(firstResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void Execute_FailingProcess_ShouldReturnFailure() {
        // Arrange
        var failingProcess = new FailingTestProcess();

        // Act
        var result = failingProcess.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(failingProcess.IsCompleted, Is.True);
    }

    [Test]
    public void Execute_WaitingProcess_ShouldReturnWaiting() {
        // Arrange
        var waitingProcess = new WaitingTestProcess();

        // Act
        var result = waitingProcess.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(waitingProcess.IsCompleted, Is.False);
    }

    [Test]
    public void Cancel_WithoutExecution_ShouldThrowException() {
        // Act & Assert
        Assert.Throws<LSException>(() => _process!.Cancel());
    }

    [Test]
    public void Cancel_AfterExecution_ShouldSetCancelledState() {
        // Arrange
        _process!.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Act
        _process.Cancel();

        // Assert
        Assert.That(_process.IsCancelled, Is.True);
        Assert.That(_process.IsCompleted, Is.True);
    }

    [Test]
    public void SetData_ShouldStoreValue() {
        // Act
        _process!.SetData("testKey", "testValue");

        // Assert
        Assert.That(_process.TryGetData<string>("testKey", out var value), Is.True);
        Assert.That(value, Is.EqualTo("testValue"));
    }

    [Test]
    public void GetData_WithValidKey_ShouldReturnValue() {
        // Arrange
        _process!.SetData("testKey", 123);

        // Act
        var value = _process.GetData<int>("testKey");

        // Assert
        Assert.That(value, Is.EqualTo(123));
    }

    [Test]
    public void GetData_WithInvalidKey_ShouldThrowException() {
        // Act & Assert
        Assert.Throws<LSException>(() => _process!.GetData<string>("nonExistentKey"));
    }

    [Test]
    public void GetData_WithWrongType_ShouldThrowException() {
        // Arrange
        _process!.SetData("testKey", "stringValue");

        // Act & Assert
        Assert.Throws<LSException>(() => _process.GetData<int>("testKey"));
    }

    [Test]
    public void TryGetData_WithValidKey_ShouldReturnTrueAndValue() {
        // Arrange
        _process!.SetData("testKey", "testValue");

        // Act
        var success = _process.TryGetData<string>("testKey", out var value);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(value, Is.EqualTo("testValue"));
    }

    [Test]
    public void TryGetData_WithInvalidKey_ShouldReturnFalse() {
        // Act
        var success = _process!.TryGetData<string>("nonExistentKey", out var value);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(value, Is.EqualTo(default(string)));
    }

    [Test]
    public void TryGetData_WithWrongType_ShouldReturnFalse() {
        // Arrange
        _process!.SetData("testKey", "stringValue");

        // Act
        var success = _process.TryGetData<int>("testKey", out var value);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(value, Is.EqualTo(default(int)));
    }

    [Test]
    public void WithProcessing_ShouldConfigureProcessingTree() {
        // Arrange
        var configured = false;

        // Act
        var result = _process!.WithProcessing(builder =>
            builder.Handler("config-test", session => {
                configured = true;
                return LSProcessResultStatus.SUCCESS;
            })
        );

        // Execute to trigger the configured processing
        result.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(configured, Is.True);
    }
}
