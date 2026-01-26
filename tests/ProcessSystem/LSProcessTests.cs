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

/// <summary>
/// Process with no predefined handlers, used for tree-building tests.
/// </summary>
internal class BasicProcess : LSProcess { }

/// <summary>
/// Process that can transition from WAITING to SUCCESS on resume for testing.
/// </summary>
internal class ResumableProcess : LSProcess {
    public int ExecutionCount { get; private set; }

    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Handler("resumable-handler", session => {
            ExecutionCount++;
            return ExecutionCount == 1 ? LSProcessResultStatus.WAITING : LSProcessResultStatus.SUCCESS;
        });
    }
}

/// <summary>
/// Sequence-based process used to validate ordered handler execution.
/// </summary>
internal class SequenceProcess : LSProcess {
    private readonly List<string> _steps;

    public SequenceProcess(List<string> steps) {
        _steps = steps;
    }

    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        return builder.Sequence("sequence-root", seq => seq
            .Handler("first", session => {
                _steps.Add("first");
                return LSProcessResultStatus.SUCCESS;
            })
            .Handler("second", session => {
                _steps.Add("second");
                return LSProcessResultStatus.SUCCESS;
            }));
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

    [Test]
    public void SequenceHandlers_ShouldRunInOrderAndSucceed() {
        // Arrange
        List<string> steps = new();
        var process = new BasicProcess();
        // this will use the default Sequence root
        process.WithProcessing(builder => {
            builder.Handler("first", session => {
                steps.Add("first");
                return LSProcessResultStatus.SUCCESS;
            });
            builder.Handler("second", session => {
                steps.Add("second");
                return LSProcessResultStatus.SUCCESS;
            });
            return builder;
        });

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(steps, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void Selector_ShouldStopAfterFirstSuccess() {
        // Arrange
        List<string> steps = new();
        var process = new BasicProcess();
        // since the default root is a Sequence, we need to explicitly use a Selector (the Selector is not root)
        process.WithProcessing(builder => builder.Selector("selector-root", sel => sel
            .Handler("fail", session => {
                steps.Add("fail");
                return LSProcessResultStatus.FAILURE;
            })
            .Handler("win", session => {
                steps.Add("win");
                return LSProcessResultStatus.SUCCESS;
            })
            .Handler("skip", session => {
                steps.Add("skip");
                return LSProcessResultStatus.SUCCESS;
            })
        ));

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(steps, Is.EqualTo(new[] { "fail", "win" }));
    }

    [Test]
    public void Inverter_ShouldInvertSuccessToFailure() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder =>
            builder.Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResultStatus.SUCCESS)
            ));

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Inverter_ShouldPropagateWaiting() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder =>
            builder.Inverter("invert", inv => inv
                .Handler("waiting-child", session => LSProcessResultStatus.WAITING)
            ));

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
    }

    [Test]
    public void Parallel_ShouldSucceedWhenSuccessThresholdMet() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder =>
            builder.Parallel("parallel-root", par => par
                .Handler("ok-1", session => LSProcessResultStatus.SUCCESS)
                .Handler("ok-2", session => LSProcessResultStatus.SUCCESS)
                .Handler("fail", session => LSProcessResultStatus.FAILURE),
                successThreshold: 2,
                failureThreshold: 2,
                priority: LSProcessPriority.NORMAL)
            );

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void Parallel_ShouldFailWhenFailureThresholdReached() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder => {
            builder.Parallel("parallel-root", par => par
                .Handler("ok-1", session => LSProcessResultStatus.SUCCESS)
                .Handler("ok-2", session => LSProcessResultStatus.SUCCESS)
                .Handler("fail", session => LSProcessResultStatus.FAILURE),
                successThreshold: 3,
                failureThreshold: 1,
                priority: LSProcessPriority.NORMAL);
            return builder;
        });

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Parallel_ResumeSpecificWaitingChildShouldSucceed() {
        // Arrange
        int waitExecutions = 0;
        var process = new BasicProcess();
        process.WithProcessing(builder => {
            builder.Parallel("parallel-root", par => par
                .Handler("wait", session => {
                    waitExecutions++;
                    return LSProcessResultStatus.WAITING;
                })
                .Handler("ok", session => LSProcessResultStatus.SUCCESS),
                successThreshold: 2,
                failureThreshold: 1,
                priority: LSProcessPriority.NORMAL);
            return builder;
        });

        // Act
        var initial = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        var resumed = process.Resume("wait");

        // Assert
        Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(resumed, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(waitExecutions, Is.EqualTo(1));
    }

    [Test]
    public void Parallel_FailSpecificWaitingChildShouldFail() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder => {
            builder.Parallel("parallel-root", par => par
                .Handler("wait", session => LSProcessResultStatus.WAITING)
                .Handler("ok", session => LSProcessResultStatus.SUCCESS),
                successThreshold: 2,
                failureThreshold: 1,
                priority: LSProcessPriority.NORMAL);
            return builder;
        });

        // Act
        var initial = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        var failed = process.Fail("wait");

        // Assert
        Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(failed, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Parallel_CancelShouldCancelWaitingChildren() {
        // Arrange
        var process = new BasicProcess();
        process.WithProcessing(builder => {
            builder.Parallel("parallel-root", par => par
                .Handler("wait", session => LSProcessResultStatus.WAITING),
                successThreshold: 1,
                failureThreshold: 1,
                priority: LSProcessPriority.NORMAL);
            return builder;
        });

        // Act
        var initial = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        process.Cancel();

        // Assert
        Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.IsCancelled, Is.True);
    }

    [Test]
    public void Conditions_ShouldSkipHandlersWhenFalse() {
        // Arrange
        bool skippedExecuted = false;
        bool executed = false;
        var process = new BasicProcess();
        process.WithProcessing(builder => {
            builder.Sequence("sequence-root", seq => seq
                .Handler("skip", session => {
                    skippedExecuted = true;
                    return LSProcessResultStatus.FAILURE;
                }, conditions: _ => false)
                .Handler("run", session => {
                    executed = true;
                    return LSProcessResultStatus.SUCCESS;
                }));
            return builder;
        });

        // Act
        var result = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(skippedExecuted, Is.False);
        Assert.That(executed, Is.True);
    }

    [Test]
    public void Resume_ShouldMoveWaitingHandlerToSuccess() {
        // Arrange
        var process = new ResumableProcess();

        // Act
        var firstResult = process.Execute(_manager!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        var resumedResult = process.Resume("resumable-handler");

        // Assert
        Assert.That(firstResult, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(resumedResult, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.ExecutionCount, Is.EqualTo(1));
    }

    [Test]
    public void NodeOverridePriority_Pipeline_MergeAndOverride_WorksCorrectly() {
        var log = new List<string>();
        var manager = new LSProcessManager();
        // Register global context
        manager.Register<TestProcessWithProcessing>(b =>
            b.Sequence("checkContextMerge", seq =>
                seq.Handler("Global", session => {
                    log.Add("Global");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
            .Sequence("override", seq =>
                seq.Handler("overridedHandler", session => {
                    log.Add("GlobalOverride");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
        );
        var instance = new TestProcessable();

        // Register instanced context
        manager.Register<TestProcessWithProcessing>(b =>
            b.Sequence("checkContextMerge", seq =>
                seq.Handler("Instanced", session => {
                    log.Add("Instanced");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
            .Sequence("override", seq =>
                seq.Handler("overridedHandler", session => {
                    log.Add("InstancedOverride");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
        , instance);

        // Custom process with processing() context
        var process = new TestProcessWithProcessing(log);

        // WithProcessing context
        process.WithProcessing(b =>
            b.Sequence("checkContextMerge", seq =>
                seq.Handler("WithProcessing", session => {
                    log.Add("WithProcessing");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
            .Sequence("override", seq =>
                seq.Handler("overridedHandler", session => {
                    log.Add("WithProcessingOverride");
                    return LSProcessResultStatus.SUCCESS;
                })
            )
        );

        var result = process.Execute(manager, LSProcessManager.ProcessInstanceBehaviour.ALL, instance);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(log, Is.EqualTo(new[] {
            "Global",
            "Instanced",
            "processing",
            "WithProcessing",
            "WithProcessingOverride"
        }));
    }
    internal class TestProcessable : ILSProcessable {
        public Guid ID { get; } = Guid.NewGuid();


        public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitialize = null, LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
            return LSProcessResultStatus.SUCCESS;
        }
    }
    internal class TestProcessWithProcessing : LSProcess {
        private readonly List<string> _log;
        public TestProcessWithProcessing(List<string> log) { _log = log; }
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
            return builder
                .Sequence("checkContextMerge", seq =>
                    seq.Handler("processing", session => {
                        _log.Add("processing");
                        return LSProcessResultStatus.SUCCESS;
                    })
                )
                .Sequence("override", seq =>
                    seq.Handler("overridedHandler", session => {
                        _log.Add("processingOverride");
                        return LSProcessResultStatus.SUCCESS;
                    })
                );
        }
    }
}
